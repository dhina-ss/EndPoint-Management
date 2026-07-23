using EMS.API.Data;
using EMS.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace EMS.API.Repositories;

public class BlockedWebsiteRepository : IBlockedWebsiteRepository
{
    private readonly ApplicationDbContext _dbContext;

    public BlockedWebsiteRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<BlockedWebsite>> GetByDeviceAsync(
        Guid deviceId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BlockedWebsites
            .AsNoTracking()
            .Where(b => b.DeviceId == deviceId)
            .OrderBy(b => b.Domain)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid deviceId, string domain, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BlockedWebsites
            .AnyAsync(b => b.DeviceId == deviceId && b.Domain == domain, cancellationToken);
    }

    public async Task<BlockedWebsite?> GetAsync(Guid deviceId, Guid blockId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BlockedWebsites
            .FirstOrDefaultAsync(b => b.DeviceId == deviceId && b.Id == blockId, cancellationToken);
    }

    public async Task AddAsync(BlockedWebsite blockedWebsite, CancellationToken cancellationToken = default)
    {
        await _dbContext.BlockedWebsites.AddAsync(blockedWebsite, cancellationToken);
    }

    public void Remove(BlockedWebsite blockedWebsite)
    {
        _dbContext.BlockedWebsites.Remove(blockedWebsite);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
