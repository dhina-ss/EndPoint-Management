using EMS.API.Entities;

namespace EMS.API.Repositories;

public interface IHeartbeatRepository
{
    Task AddAsync(DeviceHeartbeat heartbeat, CancellationToken cancellationToken = default);

    /// <summary>Most recent heartbeat for a device, or null if it has never reported.</summary>
    Task<DeviceHeartbeat?> GetLatestForDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
