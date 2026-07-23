using EMS.API.Entities;

namespace EMS.API.Repositories;

public interface IBlockedWebsiteRepository
{
    Task<IReadOnlyList<BlockedWebsite>> GetByDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid deviceId, string domain, CancellationToken cancellationToken = default);

    Task<BlockedWebsite?> GetAsync(Guid deviceId, Guid blockId, CancellationToken cancellationToken = default);

    Task AddAsync(BlockedWebsite blockedWebsite, CancellationToken cancellationToken = default);

    void Remove(BlockedWebsite blockedWebsite);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
