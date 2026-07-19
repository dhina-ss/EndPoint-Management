using EMS.API.Entities;
using EMS.API.Repositories;

namespace EMS.API.Services;

/// <summary>
/// Issues device tokens. A token is (re)issued on every successful
/// registration; the agent stores the latest one. Only the SHA-256 hash is
/// persisted, so a database leak does not expose usable credentials.
/// </summary>
public class DeviceAuthService : IDeviceAuthService
{
    private readonly IDeviceAuthRepository _authRepository;
    private readonly ILogger<DeviceAuthService> _logger;

    public DeviceAuthService(IDeviceAuthRepository authRepository, ILogger<DeviceAuthService> logger)
    {
        _authRepository = authRepository;
        _logger = logger;
    }

    public async Task<string> IssueTokenAsync(Device device, CancellationToken cancellationToken = default)
    {
        var token = DeviceTokenHasher.GenerateToken();
        var tokenHash = DeviceTokenHasher.Hash(token);
        var utcNow = DateTime.UtcNow;

        var authentication = await _authRepository.GetByDeviceKeyAsync(device.Id, cancellationToken);

        if (authentication is null)
        {
            authentication = new DeviceAuthentication
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                TokenHash = tokenHash,
                CreatedDate = utcNow,
                IsActive = true
            };

            await _authRepository.AddAsync(authentication, cancellationToken);
            _logger.LogInformation("Issued first token for device {DeviceId}.", device.DeviceId);
        }
        else
        {
            authentication.TokenHash = tokenHash;
            authentication.CreatedDate = utcNow;
            authentication.IsActive = true;
            _logger.LogInformation("Rotated token for device {DeviceId}.", device.DeviceId);
        }

        await _authRepository.SaveChangesAsync(cancellationToken);
        return token;
    }
}
