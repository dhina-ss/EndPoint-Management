using EMS.API.Data;
using EMS.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace EMS.API.Repositories;

public class ApplicationInventoryRepository : IApplicationInventoryRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ApplicationInventoryRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<InstalledApplication>> GetInstalledAsync(
        Guid deviceId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.InstalledApplications
            .AsNoTracking()
            .Where(a => a.DeviceId == deviceId)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceInstalledAsync(
        Guid deviceId, IEnumerable<InstalledApplication> applications, CancellationToken cancellationToken = default)
    {
        // A scan is authoritative: apps uninstalled since the last report
        // must disappear, so the previous set is cleared wholesale.
        await _dbContext.InstalledApplications
            .Where(a => a.DeviceId == deviceId)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.InstalledApplications.AddRangeAsync(applications, cancellationToken);
    }

    public async Task<IReadOnlyList<BlockedApplication>> GetBlockedAsync(
        Guid deviceId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BlockedApplications
            .AsNoTracking()
            .Where(b => b.DeviceId == deviceId)
            .OrderBy(b => b.ExecutableName)
            .ToListAsync(cancellationToken);
    }

    public async Task<BlockedApplication?> GetBlockedByExecutableAsync(
        Guid deviceId, string executableName, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BlockedApplications
            .FirstOrDefaultAsync(
                b => b.DeviceId == deviceId && b.ExecutableName == executableName, cancellationToken);
    }

    public async Task<BlockedApplication?> GetBlockedByIdAsync(
        Guid deviceId, Guid blockId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BlockedApplications
            .FirstOrDefaultAsync(b => b.DeviceId == deviceId && b.Id == blockId, cancellationToken);
    }

    public async Task AddBlockedAsync(BlockedApplication blocked, CancellationToken cancellationToken = default)
    {
        await _dbContext.BlockedApplications.AddAsync(blocked, cancellationToken);
    }

    public void RemoveBlocked(BlockedApplication blocked)
    {
        _dbContext.BlockedApplications.Remove(blocked);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
