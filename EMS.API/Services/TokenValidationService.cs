using EMS.API.Repositories;

namespace EMS.API.Services;

public class TokenValidationService : ITokenValidationService
{
    private readonly IDeviceAuthRepository _authRepository;
    private readonly ILogger<TokenValidationService> _logger;

    public TokenValidationService(IDeviceAuthRepository authRepository, ILogger<TokenValidationService> logger)
    {
        _authRepository = authRepository;
        _logger = logger;
    }

    public async Task<bool> ValidateDeviceTokenAsync(
        string deviceId, string token, CancellationToken cancellationToken = default)
    {
        var authentication = await _authRepository.GetByExternalDeviceIdAsync(deviceId, cancellationToken);

        if (authentication is null)
        {
            _logger.LogWarning("Token validation failed: unknown device {DeviceId}.", deviceId);
            return false;
        }

        if (!authentication.IsActive)
        {
            _logger.LogWarning("Token validation failed: credential for device {DeviceId} is deactivated.", deviceId);
            return false;
        }

        var providedHash = DeviceTokenHasher.Hash(token);
        if (!DeviceTokenHasher.HashEquals(authentication.TokenHash, providedHash))
        {
            _logger.LogWarning("Token validation failed: wrong token for device {DeviceId}.", deviceId);
            return false;
        }

        authentication.LastUsedDate = DateTime.UtcNow;
        await _authRepository.SaveChangesAsync(cancellationToken);
        return true;
    }
}
