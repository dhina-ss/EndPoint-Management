using EMS.API.Data;
using EMS.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace EMS.API.Repositories;

public class DeviceCommandRepository : IDeviceCommandRepository
{
    private readonly ApplicationDbContext _dbContext;

    public DeviceCommandRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(DeviceCommand command, CancellationToken cancellationToken = default)
    {
        await _dbContext.DeviceCommands.AddAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceCommand>> GetForDeviceAsync(
        Guid deviceId, int limit, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DeviceCommands
            .AsNoTracking()
            .Include(c => c.Package)
            .Where(c => c.DeviceId == deviceId)
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceCommand>> GetDispatchableForDeviceAsync(
        Guid deviceId, DateTime staleDispatchBefore, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DeviceCommands
            .AsNoTracking()
            .Include(c => c.Package)
            .Where(c => c.DeviceId == deviceId &&
                (c.Status == DeviceCommandStatus.Pending ||
                    (c.Status == DeviceCommandStatus.Dispatched
                        && c.DispatchedAt != null
                        && c.DispatchedAt < staleDispatchBefore)))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<DeviceCommand?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DeviceCommands
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<bool> HasActiveCommandForAppAsync(
        Guid deviceId, string appName, DateTime staleDispatchBefore, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DeviceCommands.AnyAsync(
            c => c.DeviceId == deviceId
                && c.TargetAppName == appName
                && (c.Status == DeviceCommandStatus.Pending
                    || (c.Status == DeviceCommandStatus.Dispatched
                        && (c.DispatchedAt == null || c.DispatchedAt >= staleDispatchBefore))),
            cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
