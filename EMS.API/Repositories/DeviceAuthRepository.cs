using EMS.API.Data;
using EMS.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace EMS.API.Repositories;

public class DeviceAuthRepository : IDeviceAuthRepository
{
    private readonly ApplicationDbContext _dbContext;

    public DeviceAuthRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DeviceAuthentication?> GetByExternalDeviceIdAsync(
        string deviceId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DeviceAuthentications
            .Include(a => a.Device)
            .FirstOrDefaultAsync(a => a.Device.DeviceId == deviceId, cancellationToken);
    }

    public async Task<DeviceAuthentication?> GetByDeviceKeyAsync(
        Guid deviceKey, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DeviceAuthentications
            .FirstOrDefaultAsync(a => a.DeviceId == deviceKey, cancellationToken);
    }

    public async Task AddAsync(DeviceAuthentication authentication, CancellationToken cancellationToken = default)
    {
        await _dbContext.DeviceAuthentications.AddAsync(authentication, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
