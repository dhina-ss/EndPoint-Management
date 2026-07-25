namespace EMS.Agent.Services;

/// <summary>
/// Records whether this endpoint has been activated by an EMS user sign-in.
/// The service workers read this gate; the login window writes it. Until it
/// is activated the agent does nothing (no registration, inventory,
/// heartbeat, or policy enforcement).
/// </summary>
public interface IActivationStore
{
    bool IsActivated();

    /// <summary>Records a successful activation by the given EMS username.</summary>
    void Activate(string activatedBy);

    /// <summary>The EMS username that activated this device, if any.</summary>
    string? ActivatedBy();
}
