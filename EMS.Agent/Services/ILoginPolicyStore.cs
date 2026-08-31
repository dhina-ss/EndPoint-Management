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

    /// <summary>
    /// Idle seconds before the workstation is auto-locked; 0 (or less) disables
    /// auto-lock. Set by the tenant admin, delivered via heartbeat.
    /// </summary>
    int InactivityLockSeconds();

    /// <summary>
    /// Additional idle seconds after the auto-lock before the manager is emailed.
    /// </summary>
    int InactivityAlertSeconds();

    /// <summary>Persists the latest policy values received from the server.</summary>
    void Set(bool requireLoginEachStartup, int inactivityLockSeconds, int inactivityAlertSeconds);
}
