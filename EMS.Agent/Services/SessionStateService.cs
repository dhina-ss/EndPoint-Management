using System.Runtime.Versioning;
using Microsoft.Win32;

namespace EMS.Agent.Services;

/// <summary>
/// Observes Windows session lock/unlock and power (suspend) events via
/// <see cref="SystemEvents"/>, which runs its own message-pump thread - so this
/// works in the non-UI tracker host without a manual window/loop. Lock state
/// pauses the work-time timer; the suspend event drives the sleep status beacon.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SessionStateService : ISessionStateService, IDisposable
{
    private volatile bool _isLocked;
    private readonly ILogger<SessionStateService> _logger;

    public SessionStateService(ILogger<SessionStateService> logger)
    {
        _logger = logger;

        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        _logger.LogInformation("Session-state listener started.");
    }

    public bool IsLocked => _isLocked;

    public event Action? Suspending;

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
                _isLocked = true;
                _logger.LogInformation("Session locked; work timer paused.");
                break;
            case SessionSwitchReason.SessionUnlock:
                _isLocked = false;
                _logger.LogInformation("Session unlocked; work timer resumed.");
                break;
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            _logger.LogInformation("Machine suspending; signalling sleep.");
            try
            {
                Suspending?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Suspend handler threw.");
            }
        }
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }
}
