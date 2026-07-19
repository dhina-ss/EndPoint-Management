using EMS.API.Entities;

namespace EMS.API.Repositories;

public interface IDeviceAuthRepository
{
    /// <summary>Finds a credential by the agent-facing device identifier (Device.DeviceId).</summary>
    Task<DeviceAuthentication?> GetByExternalDeviceIdAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>Finds a credential by the internal device key (Device.Id).</summary>
    Task<DeviceAuthentication?> GetByDeviceKeyAsync(Guid deviceKey, CancellationToken cancellationToken = default);

    Task AddAsync(DeviceAuthentication authentication, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
