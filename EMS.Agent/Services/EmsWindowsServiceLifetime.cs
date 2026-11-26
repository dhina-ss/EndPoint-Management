using System.Collections.Concurrent;
using System.Runtime.Versioning;
using System.ServiceProcess;
using EMS.Agent.Helpers;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;

namespace EMS.Agent.Services;

/// <summary>
/// Extends <see cref="WindowsServiceLifetime"/> so the SYSTEM Windows Service
/// directly observes Power (sleep/wake) and Session (domain logon/logoff/lock/unlock)
/// events from the Service Control Manager—guaranteeing audit logs work even for
/// restricted Active Directory domain users when per-user desktop hooks are blocked.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class EmsWindowsServiceLifetime : WindowsServiceLifetime
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmsWindowsServiceLifetime> _logger;
    private readonly ConcurrentDictionary<int, string> _sessionUsers = new();

    public EmsWindowsServiceLifetime(
        IHostEnvironment environment,
        IHostApplicationLifetime applicationLifetime,
        ILoggerFactory loggerFactory,
        IOptions<HostOptions> optionsAccessor,
        IOptions<WindowsServiceLifetimeOptions> windowsServiceOptionsAccessor,
        IServiceScopeFactory scopeFactory)
        : base(environment, applicationLifetime, loggerFactory, optionsAccessor, windowsServiceOptionsAccessor)
    {
        _scopeFactory = scopeFactory;
        _logger = loggerFactory.CreateLogger<EmsWindowsServiceLifetime>();

        CanHandlePowerEvent = true;
        CanHandleSessionChangeEvent = true;
        CanShutdown = true;
    }

    protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
    {
        if (powerStatus == PowerBroadcastStatus.Suspend)
        {
            _logger.LogInformation("SCM power suspend event; reporting sleep.");
            // Give the sleep beacon up to 1.5s to flush before Windows freezes the NIC.
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
                SendPowerStateOnceAsync(suspended: true, cts.Token).Wait(1600);
            }
            catch { /* best effort before suspend */ }
        }
        else if (powerStatus == PowerBroadcastStatus.ResumeSuspend ||
                 powerStatus == PowerBroadcastStatus.ResumeAutomatic)
        {
            _logger.LogInformation("SCM power resume event ({Status}); reporting wake.", powerStatus);
            DispatchWakePowerState();
        }

        return base.OnPowerEvent(powerStatus);
    }

    protected override void OnShutdown()
    {
        _logger.LogInformation("SCM shutdown event; reporting shutdown.");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            SendSessionEventOnceAsync("shutdown", "Windows shutting down", cts.Token).Wait(1800);
        }
        catch { /* best effort during shutdown */ }

        base.OnShutdown();
    }

    protected override void OnSessionChange(SessionChangeDescription changeDescription)
    {
        var sessionId = changeDescription.SessionId;
        var username = WtsSessionHelper.GetSessionUsername(sessionId);
        if (!string.IsNullOrWhiteSpace(username))
        {
            _sessionUsers[sessionId] = username;
        }
        else if (_sessionUsers.TryGetValue(sessionId, out var cached))
        {
            username = cached;
        }

        string? eventType = changeDescription.Reason switch
        {
            SessionChangeReason.SessionLogon => "login",
            SessionChangeReason.SessionLogoff => "logoff",
            SessionChangeReason.SessionLock => "lock",
            SessionChangeReason.RemoteDisconnect => "lock",
            SessionChangeReason.ConsoleDisconnect => "lock",
            SessionChangeReason.SessionUnlock => "unlock",
            SessionChangeReason.RemoteConnect => "unlock",
            SessionChangeReason.ConsoleConnect => "unlock",
            _ => null
        };

        if (eventType is not null)
        {
            _logger.LogInformation(
                "SCM session change: {Reason} (session {SessionId}, user {User}).",
                changeDescription.Reason, sessionId, username ?? "unknown");
            DispatchSessionEvent(eventType, username, retryOnWakeDelay: eventType is "unlock" or "login");

            if (changeDescription.Reason == SessionChangeReason.SessionLogoff)
            {
                _sessionUsers.TryRemove(sessionId, out _);
            }
        }

        base.OnSessionChange(changeDescription);
    }

    private void DispatchWakePowerState()
    {
        _ = Task.Run(async () =>
        {
            await SendPowerStateOnceAsync(suspended: false, CancellationToken.None);
            // Wi-Fi / domain NIC often reconnects a few seconds after ResumeSuspend;
            // send a delayed backup beacon (server 30s dedup drops duplicates).
            await Task.Delay(TimeSpan.FromSeconds(5));
            await SendPowerStateOnceAsync(suspended: false, CancellationToken.None);
        });
    }

    private async Task SendPowerStateOnceAsync(bool suspended, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var activation = scope.ServiceProvider.GetRequiredService<IActivationStore>();
            if (!activation.IsActivated()) return;

            var apiClient = scope.ServiceProvider.GetRequiredService<IApiClientService>();
            await apiClient.SendPowerStateAsync(suspended, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SCM power-state report failed.");
        }
    }

    private void DispatchSessionEvent(string eventType, string? detail, bool retryOnWakeDelay = false)
    {
        _ = Task.Run(async () =>
        {
            await SendSessionEventOnceAsync(eventType, detail, CancellationToken.None);
            if (retryOnWakeDelay)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                await SendSessionEventOnceAsync(eventType, detail, CancellationToken.None);
            }
        });
    }

    private async Task SendSessionEventOnceAsync(string eventType, string? detail, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var activation = scope.ServiceProvider.GetRequiredService<IActivationStore>();
            if (!activation.IsActivated()) return;

            var apiClient = scope.ServiceProvider.GetRequiredService<IApiClientService>();
            await apiClient.SendSessionEventAsync(eventType, detail, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SCM session-event ({Type}) report failed.", eventType);
        }
    }
}
