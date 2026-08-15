namespace EMS.Agent.Services;

/// <summary>
/// Records whether this endpoint has been activated by an EMS user sign-in.
/// The service workers read this gate; the login window writes it. Until it
/// is activated the agent does nothing (no registration, inventory,
/// heartbeat, or policy enforcement).
/// </summary>
public interface IActivationStore
{
    /// <summary>
    /// True once this device has ever been activated (enrolled). Independent of
    /// the per-startup sign-in policy; the heartbeat uses this so an enrolled
    /// device stays manageable and keeps receiving policy.
    /// </summary>
    bool IsActivated();

    /// <summary>Records a successful activation by the given EMS username.</summary>
    void Activate(string activatedBy);

    /// <summary>The EMS username that activated this device, if any.</summary>
    string? ActivatedBy();

    /// <summary>
    /// The boot session in which the last sign-in happened (OS boot id). Used
    /// with the require-login-each-startup policy to detect a new power-on.
    /// </summary>
    string? SessionBootId();
}
