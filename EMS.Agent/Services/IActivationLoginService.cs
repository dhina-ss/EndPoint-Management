namespace EMS.Agent.Services;

/// <summary>
/// Verifies EMS credentials against the server and, on success, activates
/// this device. Used by the login window.
/// </summary>
public interface IActivationLoginService
{
    Task<ActivationLoginResult> LoginAndActivateAsync(
        string usernameOrEmail, string password, CancellationToken cancellationToken = default);
}

public sealed record ActivationLoginResult(bool Success, string Message);
