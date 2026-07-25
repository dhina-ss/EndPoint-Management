using EMS.API.DTOs;

namespace EMS.API.Services;

public interface IAuthService
{
    /// <summary>
    /// Verifies an EMS user's credentials. Returns a response with
    /// Success=false (and a generic message) for both unknown users and wrong
    /// passwords, so callers cannot probe which usernames exist.
    /// </summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
