namespace EMS.Agent.Services;

/// <summary>
/// Decides whether the agent should be actively monitoring this session,
/// combining enrollment (<see cref="IActivationStore"/>) with the
/// require-login-each-startup policy (<see cref="ILoginPolicyStore"/>).
/// </summary>
public interface IActivationGate
{
    /// <summary>
    /// True when the device is enrolled and — if the policy requires sign-in on
    /// every startup — the user has signed in during the current power-on
    /// session. When the policy is off this is just enrollment (one-time).
    /// </summary>
    bool IsMonitoringActive();

    /// <summary>
    /// True when the device is enrolled but a fresh sign-in is required this
    /// boot (every-startup policy is on and this session hasn't signed in yet).
    /// Signals the tracker to launch the login window.
    /// </summary>
    bool NeedsSessionLogin();
}
