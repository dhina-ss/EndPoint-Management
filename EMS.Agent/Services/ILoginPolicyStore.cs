namespace EMS.Agent.Services;

/// <summary>
/// File-backed store of the device's "require sign-in on every startup"
/// policy: ProgramData\EMS.Agent\login-policy.json. The SYSTEM service writes
/// it from each heartbeat; the per-user tracker and login window read it to
/// decide whether to re-prompt for sign-in on this boot.
/// </summary>
public interface ILoginPolicyStore
{
    /// <summary>True when the user must sign in on every device startup.</summary>
    bool RequireLoginEachStartup();

    /// <summary>Persists the latest policy value received from the server.</summary>
    void Set(bool requireLoginEachStartup);
}
