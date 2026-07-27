using EMS.API.Entities;

namespace EMS.API.Repositories;

/// <summary>Storage for the per-device software-management command queue.</summary>
public interface IDeviceCommandRepository
{
    Task AddAsync(DeviceCommand command, CancellationToken cancellationToken = default);

    /// <summary>Recent commands for a device (newest first), package included.</summary>
    Task<IReadOnlyList<DeviceCommand>> GetForDeviceAsync(
        Guid deviceId, int limit, CancellationToken cancellationToken = default);

    /// <summary>Pending commands for a device, with package metadata, oldest first.</summary>
    Task<IReadOnlyList<DeviceCommand>> GetPendingForDeviceAsync(
        Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>Tracked command by id, for recording a result.</summary>
    Task<DeviceCommand?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>True while a device has an app command that is not yet finished.</summary>
    Task<bool> HasActiveCommandForAppAsync(
        Guid deviceId, string appName, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
