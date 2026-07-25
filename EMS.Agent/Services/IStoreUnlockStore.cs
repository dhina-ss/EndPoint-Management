namespace EMS.Agent.Services;

/// <summary>
/// Shared unlock-window state for Microsoft Store gating. The per-user unlock
/// process writes an expiry (after verifying the admin password); the SYSTEM
/// service reads it to decide whether the Store should currently be enabled.
/// </summary>
public interface IStoreUnlockStore
{
    /// <summary>True when an unlock is currently active (expiry in the future).</summary>
    bool IsUnlockActive();

    /// <summary>Records an unlock valid until now + the given duration.</summary>
    void GrantUnlock(TimeSpan duration);
}
