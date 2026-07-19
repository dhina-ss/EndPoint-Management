using EMS.API.Entities;

namespace EMS.API.Repositories;

public interface IHeartbeatRepository
{
    Task AddAsync(DeviceHeartbeat heartbeat, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
