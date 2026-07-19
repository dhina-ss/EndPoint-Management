using EMS.API.Data;
using EMS.API.Entities;

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

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
