using EMS.API.Data;
using EMS.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace EMS.API.Repositories;

public class DeviceRepository : IDeviceRepository
{
    private readonly ApplicationDbContext _dbContext;

    public DeviceRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Device?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Devices
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId, cancellationToken);
    }

    public async Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Devices
            .AsNoTracking()
            .Include(d => d.ActivatedByUser)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<Device?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Devices
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Device>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Devices
            .AsNoTracking()
            .Include(d => d.ActivatedByUser)
            .OrderBy(d => d.DeviceName)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Device device, CancellationToken cancellationToken = default)
    {
        await _dbContext.Devices.AddAsync(device, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
