using EMS.API.Entities;

namespace EMS.API.Repositories;

public interface IDeviceRepository
{
    Task<Device?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken = default);

    Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Device>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Device device, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
