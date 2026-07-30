using EMS.API.Data;
using EMS.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace EMS.API.Repositories;

public class WorkSessionRepository : IWorkSessionRepository
{
    private readonly ApplicationDbContext _dbContext;

    public WorkSessionRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WorkSessionRecord?> GetTrackedAsync(
        Guid deviceId, DateOnly workDate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.WorkSessionRecords.FirstOrDefaultAsync(
            w => w.DeviceId == deviceId && w.WorkDate == workDate, cancellationToken);
    }

    public async Task AddAsync(WorkSessionRecord record, CancellationToken cancellationToken = default)
    {
        await _dbContext.WorkSessionRecords.AddAsync(record, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkSessionRecord>> GetByDeviceSinceAsync(
        Guid deviceId, DateOnly fromDate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.WorkSessionRecords
            .AsNoTracking()
            .Where(w => w.DeviceId == deviceId && w.WorkDate >= fromDate)
            .OrderByDescending(w => w.WorkDate)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
