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

    public async Task<InstalledApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.InstalledApplications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
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

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
