using System.Management;
using System.Runtime.Versioning;
using EMS.Agent.Helpers;

namespace EMS.Agent.Workers;

/// <summary>
/// Runs inside the LocalSystem Windows Service and ensures every active
/// interactive user session (including non-admin Active Directory domain users)
/// has <c>EMS.Agent.exe --usage-tracker</c> running—even if installed under
/// another admin account, after silent auto-update, or when Startup Run keys
/// are restricted by Group Policy.
/// </summary>
[SupportedOSPlatform("windows")]
public class SessionWatchdogWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(25);
    private readonly ILogger<SessionWatchdogWorker> _logger;

    public SessionWatchdogWorker(ILogger<SessionWatchdogWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("User-session tracker watchdog started.");
        try
        {
            // Give logon tasks / HKLM Run a brief head start before checking.
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                EnsureTrackersRunning();
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private void EnsureTrackersRunning()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                return;
            }

            var activeSessions = WtsSessionHelper.GetActiveUserSessions();
            if (activeSessions.Count == 0)
            {
                return;
            }

            var trackerSessions = GetRunningTrackerSessionIds();
            foreach (var session in activeSessions)
            {
                if (trackerSessions.Contains(session.SessionId))
                {
                    continue;
                }

                // Make sure ProgramData ACLs allow the active domain/local user
                // to read device-auth.json & device-id.json before launching.
                ProgramDataPermissions.EnsureUserWritable(_logger);

                _logger.LogInformation(
                    "No --usage-tracker running in active session {SessionId} ({User}); starting it.",
                    session.SessionId, session.Username ?? "unknown");
                WtsSessionHelper.LaunchInUserSession(session.SessionId, exePath, "--usage-tracker", _logger);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Session watchdog tick failed.");
        }
    }

    private static HashSet<int> GetRunningTrackerSessionIds()
    {
        var sessions = new HashSet<int>();
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

        return sessions;
    }
}
