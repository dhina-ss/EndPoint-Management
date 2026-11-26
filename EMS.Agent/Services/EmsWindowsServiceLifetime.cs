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
            DispatchPowerState(suspended: true);
        }
        else if (powerStatus == PowerBroadcastStatus.ResumeSuspend ||
                 powerStatus == PowerBroadcastStatus.ResumeAutomatic)
        {
            _logger.LogInformation("SCM power resume event ({Status}); reporting wake.", powerStatus);
            DispatchPowerState(suspended: false);
        }

        return base.OnPowerEvent(powerStatus);
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
            SessionChangeReason.SessionUnlock => "unlock",
            _ => null
        };

        if (eventType is not null)
        {
            _logger.LogInformation(
                "SCM session change: {Reason} (session {SessionId}, user {User}).",
                changeDescription.Reason, sessionId, username ?? "unknown");
            DispatchSessionEvent(eventType, username);

            if (changeDescription.Reason == SessionChangeReason.SessionLogoff)
            {
                _sessionUsers.TryRemove(sessionId, out _);
            }
        }

        base.OnSessionChange(changeDescription);
    }

    private void DispatchPowerState(bool suspended)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var activation = scope.ServiceProvider.GetRequiredService<IActivationStore>();
                if (!activation.IsActivated()) return;

                var apiClient = scope.ServiceProvider.GetRequiredService<IApiClientService>();
                await apiClient.SendPowerStateAsync(suspended);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SCM power-state report failed.");
            }
        });
    }

    private void DispatchSessionEvent(string eventType, string? detail)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var activation = scope.ServiceProvider.GetRequiredService<IActivationStore>();
                if (!activation.IsActivated()) return;

                var apiClient = scope.ServiceProvider.GetRequiredService<IApiClientService>();
                await apiClient.SendSessionEventAsync(eventType, detail);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SCM session-event ({Type}) report failed.", eventType);
            }
        });
    }
}
