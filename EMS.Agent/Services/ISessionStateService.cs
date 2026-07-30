namespace EMS.Agent.Services;

/// <summary>
/// Tracks the interactive session's lock state and raises an event when the
/// machine is about to sleep. Only meaningful in the per-user tracker process,
/// which runs inside the user's session where these events are observable.
/// </summary>
public interface ISessionStateService
{
    /// <summary>True while the workstation is locked (screen locked).</summary>
    bool IsLocked { get; }

    /// <summary>Raised just before the machine suspends (sleep/hibernate).</summary>
    event Action? Suspending;
}
