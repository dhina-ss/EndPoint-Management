using EMS.Agent.Helpers;

namespace EMS.Agent.Services;

/// <inheritdoc cref="IActivationGate"/>
public class ActivationGate : IActivationGate
{
    private readonly IActivationStore _activationStore;
    private readonly ILoginPolicyStore _loginPolicyStore;

    public ActivationGate(IActivationStore activationStore, ILoginPolicyStore loginPolicyStore)
    {
        _activationStore = activationStore;
        _loginPolicyStore = loginPolicyStore;
    }

    public bool IsMonitoringActive()
    {
        if (!_activationStore.IsActivated())
        {
            return false;
        }

        if (!_loginPolicyStore.RequireLoginEachStartup())
        {
            return true; // One-time activation: enrolled is enough.
        }

        return SignedInThisSession();
    }

    public bool NeedsSessionLogin()
        => _activationStore.IsActivated()
            && _loginPolicyStore.RequireLoginEachStartup()
            && !SignedInThisSession();

    /// <summary>
    /// True when the recorded sign-in belongs to the current power-on session.
    /// If the boot id can't be read, fail safe by treating the session as
    /// signed-in (don't lock the user out over a transient WMI read failure).
    /// </summary>
    private bool SignedInThisSession()
    {
        var currentBootId = SystemInfoHelper.GetBootId();
        if (currentBootId is null)
        {
            return true;
        }

        return string.Equals(_activationStore.SessionBootId(), currentBootId, StringComparison.Ordinal);
    }
}
