using EMS.API.Data;
using EMS.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace EMS.API.Repositories;

public class HeartbeatRepository : IHeartbeatRepository
{
    private readonly ApplicationDbContext _dbContext;

    public HeartbeatRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(DeviceHeartbeat heartbeat, CancellationToken cancellationToken = default)
    {
        await _dbContext.DeviceHeartbeats.AddAsync(heartbeat, cancellationToken);
    }

    public async Task<DeviceHeartbeat?> GetLatestForDeviceAsync(
        Guid deviceId, CancellationToken cancellationToken = default)
    {
        // Served by the existing (DeviceId, HeartbeatTime) index.
        return await _dbContext.DeviceHeartbeats
            .AsNoTracking()
            .Where(h => h.DeviceId == deviceId)
            .OrderByDescending(h => h.HeartbeatTime)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
