using EMS.API.Data;
using EMS.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace EMS.API.Repositories;

public class DeviceThreatRepository : IDeviceThreatRepository
{
    private readonly ApplicationDbContext _dbContext;

    public DeviceThreatRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<DeviceThreat>> GetByDeviceAsync(
        Guid deviceId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DeviceThreats
            .AsNoTracking()
            .Where(t => t.DeviceId == deviceId)
            .OrderByDescending(t => t.DetectedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceForDeviceAsync(
        Guid deviceId, IEnumerable<DeviceThreat> threats, CancellationToken cancellationToken = default)
    {
        // A report reflects Defender's whole current history, so the prior
        // set is cleared wholesale before inserting the new one.
        await _dbContext.DeviceThreats
            .Where(t => t.DeviceId == deviceId)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.DeviceThreats.AddRangeAsync(threats, cancellationToken);
    }

    public async Task<int> CountActiveAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DeviceThreats
            .AsNoTracking()
            .CountAsync(t => t.DeviceId == deviceId && !t.Remediated, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
