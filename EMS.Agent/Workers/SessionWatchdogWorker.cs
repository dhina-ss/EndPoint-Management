using System.Management;
using System.Runtime.Versioning;
using EMS.Agent.Helpers;
using EMS.Agent.Services;

namespace EMS.Agent.Workers;

/// <summary>
/// Runs inside the LocalSystem Windows Service and ensures every active
/// interactive user session (including non-admin Active Directory domain users)
/// has <c>EMS.Agent.exe --usage-tracker</c> running—even if installed under
/// another admin account, after silent auto-update, or when Startup Run keys
/// are restricted by Group Policy. Also handles LocalSystem session-lock
/// fallback and idle RDP disconnects for server/domain users.
/// </summary>
[SupportedOSPlatform("windows")]
public class SessionWatchdogWorker : BackgroundService
{
    private static readonly TimeSpan FastPollInterval = TimeSpan.FromSeconds(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoginPolicyStore _policyStore;
    private readonly IActivationGate _gate;
    private readonly ILogger<SessionWatchdogWorker> _logger;
    private readonly HashSet<int> _rdpAutoLockedSessions = new();
    private readonly Dictionary<int, bool> _sessionLockedStates = new();
    private readonly Dictionary<int, string?> _sessionUsernames = new();

    public SessionWatchdogWorker(
        IServiceScopeFactory scopeFactory,
        ILoginPolicyStore policyStore,
        IActivationGate gate,
        ILogger<SessionWatchdogWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _policyStore = policyStore;
        _gate = gate;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("User-session tracker watchdog started.");
        try
        {
            // Give logon tasks / HKLM Run a brief head start before checking.
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            var tick = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessPendingLockRequestsAsync(stoppingToken);

                if (tick % 3 == 0)
                {
                    await EnsureTrackersAndRdpIdleLockAsync(stoppingToken);
                }

                tick++;
                await Task.Delay(FastPollInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task ProcessPendingLockRequestsAsync(CancellationToken cancellationToken)
    {
        var requestedSessions = WtsSessionHelper.ConsumePendingLockRequests(_logger);
        if (requestedSessions.Count == 0)
        {
            return;
        }

        foreach (var sessionId in requestedSessions)
        {
            var ext = WtsSessionHelper.GetSessionExtendedState(sessionId);
            if (ext?.IsLocked == true)
            {
                continue;
            }

            if (WtsSessionHelper.DisconnectSession(sessionId, _logger))
            {
                await SendIdleLockBeaconAsync(cancellationToken);
            }
        }
    }

    private async Task EnsureTrackersAndRdpIdleLockAsync(CancellationToken cancellationToken)
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                return;
            }

            var activeSessions = WtsSessionHelper.GetActiveUserSessions();
            var activeIds = activeSessions.Select(s => s.SessionId).ToHashSet();
            _rdpAutoLockedSessions.RemoveWhere(id => !activeIds.Contains(id));

            // Check any previously tracked sessions that are no longer active (e.g. RDP disconnected/locked).
            foreach (var knownId in _sessionLockedStates.Keys.ToList())
            {
                if (activeIds.Contains(knownId)) continue;
                var prevExt = WtsSessionHelper.GetSessionExtendedState(knownId);
                if (prevExt is null)
                {
                    _sessionLockedStates.Remove(knownId);
                    _sessionUsernames.Remove(knownId);
                    continue;
                }

                if (prevExt.IsLocked == true && (!_sessionLockedStates.TryGetValue(knownId, out var wasLocked) || !wasLocked))
                {
                    _sessionLockedStates[knownId] = true;
                    _sessionUsernames.TryGetValue(knownId, out var uname);
                    await SendSessionEventBeaconAsync("lock", uname, cancellationToken);
                }
            }

            if (activeSessions.Count == 0)
            {
                return;
            }

            var trackerSessions = GetRunningTrackerSessionIds(_logger);
            var lockSeconds = GetLockThresholdSeconds();

            foreach (var session in activeSessions)
            {
                if (!string.IsNullOrWhiteSpace(session.Username))
                {
                    _sessionUsernames[session.SessionId] = session.Username;
                }

                if (!trackerSessions.Contains(session.SessionId))
                {
                    // Make sure ProgramData ACLs allow the active domain/local user
                    // to read device-auth.json, device-id.json & login-policy.json before launching.
                    ProgramDataPermissions.EnsureUserWritable(_logger);

                    _logger.LogInformation(
                        "No --usage-tracker running in active session {SessionId} ({User}); starting it.",
                        session.SessionId, session.Username ?? "unknown");
                    WtsSessionHelper.LaunchInUserSession(session.SessionId, exePath, "--usage-tracker", _logger);
                }

                var ext = WtsSessionHelper.GetSessionExtendedState(session.SessionId);
                if (ext?.IsLocked is bool nowLocked)
                {
                    if (_sessionLockedStates.TryGetValue(session.SessionId, out var prevLocked))
                    {
                        if (prevLocked && !nowLocked)
                        {
                            _logger.LogInformation(
                                "Session {SessionId} ({User}) unlocked/resumed; reporting unlock.",
                                session.SessionId, session.Username ?? "unknown");
                            await SendSessionEventBeaconAsync("unlock", session.Username, cancellationToken);
                        }
                        else if (!prevLocked && nowLocked)
                        {
                            await SendSessionEventBeaconAsync("lock", session.Username, cancellationToken);
                        }
                    }

                    _sessionLockedStates[session.SessionId] = nowLocked;
                }

                if (lockSeconds > 0 && ext is { IsRemote: true, IsLocked: false, IdleSeconds: int rdpIdle })
                {
                    if (rdpIdle < lockSeconds)
                    {
                        _rdpAutoLockedSessions.Remove(session.SessionId);
                    }
                    else if (rdpIdle >= lockSeconds + 15 && _rdpAutoLockedSessions.Add(session.SessionId))
                    {
                        _logger.LogInformation(
                            "RDP domain session {SessionId} ({User}) idle {Idle}s >= {Threshold}s; disconnecting/locking session from service.",
                            session.SessionId, session.Username ?? "unknown", rdpIdle, lockSeconds);
                        if (WtsSessionHelper.DisconnectSession(session.SessionId, _logger))
                        {
                            _sessionLockedStates[session.SessionId] = true;
                            await SendIdleLockBeaconAsync(cancellationToken);
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Session watchdog tick failed.");
        }
    }

    private int GetLockThresholdSeconds()
    {
        try
        {
            if (!_gate.IsMonitoringActive())
            {
                return 0;
            }

            return _policyStore.InactivityLockSeconds();
        }
        catch
        {
            return 0;
        }
    }

    private async Task SendIdleLockBeaconAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var apiClient = scope.ServiceProvider.GetRequiredService<IApiClientService>();
            await apiClient.SendIdleLockAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Service idle-lock beacon failed.");
        }
    }

    private async Task SendSessionEventBeaconAsync(string eventType, string? detail, CancellationToken cancellationToken)
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
            _logger.LogDebug(ex, "Service session-event ({EventType}) beacon failed.", eventType);
        }
    }

    private static HashSet<int> GetRunningTrackerSessionIds(ILogger logger)
    {
        var sessions = new HashSet<int>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT SessionId, CommandLine FROM Win32_Process WHERE Name = 'EMS.Agent.exe'");
            foreach (var proc in searcher.Get())
            {
                var cmd = proc["CommandLine"]?.ToString() ?? string.Empty;
                if (!cmd.Contains("--usage-tracker", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (proc["SessionId"] is uint sid)
                {
                    sessions.Add((int)sid);
                }
                else if (int.TryParse(proc["SessionId"]?.ToString(), out var parsed))
                {
                    sessions.Add(parsed);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not query running tracker processes via WMI.");
        }

        return sessions;
    }
}
