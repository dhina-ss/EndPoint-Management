using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using EMS.Agent.Helpers;
using EMS.Agent.Services;

namespace EMS.Agent.Workers;

/// <summary>
/// Per-user inactivity guard. Runs in the interactive tracker process (the only
/// context that can read the signed-in user's idle time and lock their session).
///
/// When the user has been idle for the tenant's configured time it locks the
/// workstation and beacons a 'lock' event. If the session then stays locked past
/// the alert window without the user returning, it beacons an 'idle-alert' once,
/// which the server turns into an email to the user's assigned manager. Returning
/// (unlocking) resets the cycle.
///
/// Thresholds are delivered by heartbeat and shared through
/// <see cref="ILoginPolicyStore"/>; a lock time of 0 disables auto-lock.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class IdleLockWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoginPolicyStore _policyStore;
    private readonly ISessionStateService _sessionState;
    private readonly IActivationGate _gate;
    private readonly ILogger<IdleLockWorker> _logger;

    private bool _autoLocked;
    private bool _alerted;
    private DateTime _autoLockedAtUtc;

    public IdleLockWorker(
        IServiceScopeFactory scopeFactory,
        ILoginPolicyStore policyStore,
        ISessionStateService sessionState,
        IActivationGate gate,
        ILogger<IdleLockWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _policyStore = policyStore;
        _sessionState = sessionState;
        _gate = gate;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Idle-lock worker started (poll {Seconds}s).", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollInterval, stoppingToken);
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Idle-lock tick failed.");
            }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        // Fail-open on a gate hiccup, but don't lock while monitoring is off
        // (not enrolled, or a require-login session that hasn't signed in yet).
        if (!MonitoringActive())
        {
            _autoLocked = false;
            _alerted = false;
            return;
        }

        var lockSeconds = _policyStore.InactivityLockSeconds();
        if (lockSeconds <= 0)
        {
            // Auto-lock disabled by policy.
            _autoLocked = false;
            _alerted = false;
            return;
        }

        var sessionId = Process.GetCurrentProcess().SessionId;
        var alertSeconds = _policyStore.InactivityAlertSeconds();
        var idleSeconds = GetIdleSeconds();
        var isLocked = WtsSessionHelper.IsSessionLocked(sessionId) ?? _sessionState.IsLocked;

        if (!_autoLocked)
        {
            if (!isLocked && idleSeconds >= lockSeconds)
            {
                _logger.LogInformation(
                    "Idle {Idle}s >= {Threshold}s (session {SessionId}); locking workstation.",
                    idleSeconds, lockSeconds, sessionId);
                if (await TryLockSessionAsync(sessionId, cancellationToken))
                {
                    _autoLocked = true;
                    _alerted = false;
                    _autoLockedAtUtc = DateTime.UtcNow;
                    await BeaconAsync(idleLock: true, cancellationToken);
                }
            }
            return;
        }

        // Already auto-locked: the user returning (unlock) resets the cycle.
        if (!isLocked)
        {
            _autoLocked = false;
            _alerted = false;
            return;
        }

        // Still locked: re-send the lock beacon each tick so the server keeps the
        // device shown as Sleep. Once the user unlocks we stop sending and the
        // server's freshness window lets the status return to Online on its own.
        // The server dedups the timeline entry, so this refresh adds no log spam.
        await BeaconAsync(idleLock: true, cancellationToken);

        if (!_alerted && alertSeconds > 0
            && (DateTime.UtcNow - _autoLockedAtUtc).TotalSeconds >= alertSeconds)
        {
            _logger.LogInformation("Locked {Alert}s without return; alerting manager.", alertSeconds);
            _alerted = true;
            await BeaconAsync(idleLock: false, cancellationToken);
        }
    }

    private async Task BeaconAsync(bool idleLock, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var apiClient = scope.ServiceProvider.GetRequiredService<IApiClientService>();
            if (idleLock)
            {
                await apiClient.SendIdleLockAsync(cancellationToken);
            }
            else
            {
                await apiClient.SendIdleAlertAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Idle beacon ({Kind}) failed.", idleLock ? "lock" : "alert");
        }
    }

    private async Task<bool> TryLockSessionAsync(int sessionId, CancellationToken cancellationToken)
    {
        var apiOk = LockWorkStation();
        var err = apiOk ? 0 : Marshal.GetLastWin32Error();
        if (!apiOk)
        {
            _logger.LogWarning("LockWorkStation returned false (Win32 error {Error}) in session {SessionId}; attempting WTS disconnect fallback.",
                err, sessionId);
        }

        // Give Windows a brief moment to switch desktops and update WTSSessionInfoEx lock flags.
        await Task.Delay(TimeSpan.FromMilliseconds(1200), cancellationToken);
        var kernelLocked = WtsSessionHelper.IsSessionLocked(sessionId);
        if (kernelLocked == true)
        {
            return true;
        }

        // In RDP / Windows Server domain sessions or under DisableLockWorkstation GPO,
        // LockWorkStation can return false or leave the session unlocked. Disconnecting
        // the session locks it behind the Windows sign-in screen.
        if (!apiOk || kernelLocked == false)
        {
            if (WtsSessionHelper.DisconnectSession(-1, _logger))
            {
                return true;
            }

            // Standard domain user may lack permission to disconnect their own RDP session;
            // hand off to the LocalSystem service watchdog.
            if (WtsSessionHelper.RequestServiceLock(sessionId, _logger))
            {
                _logger.LogInformation("Requested LocalSystem service to lock/disconnect session {SessionId}.", sessionId);
                return true;
            }
        }

        return apiOk;
    }

    private bool MonitoringActive()
    {
        try { return _gate.IsMonitoringActive(); }
        catch { return true; }
    }

    /// <summary>Seconds since the last keyboard/mouse input in this session.</summary>
    private static int GetIdleSeconds()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info))
        {
            return 0;
        }

        // Both are 32-bit tick counts; unchecked subtraction handles wraparound.
        var idleMs = unchecked((uint)Environment.TickCount - info.dwTime);
        return (int)(idleMs / 1000u);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LockWorkStation();
}
