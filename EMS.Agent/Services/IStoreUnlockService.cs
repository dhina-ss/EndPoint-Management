namespace EMS.Agent.Services;

/// <summary>
/// Verifies an EMS admin's credentials and, on success, grants a temporary
/// Microsoft Store unlock. Used by the unlock window.
/// </summary>
public interface IStoreUnlockService
{
    Task<StoreUnlockResult> UnlockAsync(
        string employeeCode, string password, CancellationToken cancellationToken = default);
}

public sealed record StoreUnlockResult(bool Success, string Message);
