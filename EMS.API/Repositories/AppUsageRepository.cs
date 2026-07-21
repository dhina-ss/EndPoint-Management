using EMS.API.Data;
using EMS.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace EMS.API.Repositories;

public class AppUsageRepository : IAppUsageRepository
{
    private readonly ApplicationDbContext _dbContext;

    public AppUsageRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AppUsageRecord?> GetTrackedAsync(
        Guid deviceId, string applicationName, DateOnly usageDate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AppUsageRecords.FirstOrDefaultAsync(
            a => a.DeviceId == deviceId && a.ApplicationName == applicationName && a.UsageDate == usageDate,
            cancellationToken);
    }

    public async Task AddAsync(AppUsageRecord record, CancellationToken cancellationToken = default)
    {
        await _dbContext.AppUsageRecords.AddAsync(record, cancellationToken);
    }

    public async Task<IReadOnlyList<AppUsageRecord>> GetByDeviceAndDateAsync(
        Guid deviceId, DateOnly usageDate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AppUsageRecords
            .AsNoTracking()
            .Where(a => a.DeviceId == deviceId && a.UsageDate == usageDate)
            .OrderByDescending(a => a.DurationSeconds)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
