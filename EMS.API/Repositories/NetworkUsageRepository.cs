using EMS.API.Data;
using EMS.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace EMS.API.Repositories;

public class NetworkUsageRepository : INetworkUsageRepository
{
    private readonly ApplicationDbContext _dbContext;

    public NetworkUsageRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<NetworkUsageRecord?> GetTrackedAsync(
        Guid deviceId, DateOnly usageDate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.NetworkUsageRecords.FirstOrDefaultAsync(
            n => n.DeviceId == deviceId && n.UsageDate == usageDate, cancellationToken);
    }

    public async Task AddAsync(NetworkUsageRecord record, CancellationToken cancellationToken = default)
    {
        await _dbContext.NetworkUsageRecords.AddAsync(record, cancellationToken);
    }

    public async Task<IReadOnlyList<NetworkUsageRecord>> GetByDeviceSinceAsync(
        Guid deviceId, DateOnly fromDate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.NetworkUsageRecords
            .AsNoTracking()
            .Where(n => n.DeviceId == deviceId && n.UsageDate >= fromDate)
            .OrderByDescending(n => n.UsageDate)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
