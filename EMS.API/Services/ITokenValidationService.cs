namespace EMS.API.Services;

public interface ITokenValidationService
{
    /// <summary>
    /// Returns true when the token matches the active credential of the given
    /// device. Updates the credential's LastUsedDate on success.
    /// </summary>
    Task<bool> ValidateDeviceTokenAsync(string deviceId, string token, CancellationToken cancellationToken = default);
}
