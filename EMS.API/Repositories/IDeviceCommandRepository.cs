using EMS.API.Entities;

namespace EMS.API.Repositories;

/// <summary>Storage for the per-device software-management command queue.</summary>
public interface IDeviceCommandRepository
{
    Task AddAsync(DeviceCommand command, CancellationToken cancellationToken = default);

    /// <summary>Recent commands for a device (newest first), package included.</summary>
    Task<IReadOnlyList<DeviceCommand>> GetForDeviceAsync(
        Guid deviceId, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commands to hand a device: Pending ones, plus Dispatched ones that were
    /// handed out before <paramref name="staleDispatchBefore"/> and never
    /// reported back (a crashed/replaced agent), so they get retried.
    /// </summary>
    Task<IReadOnlyList<DeviceCommand>> GetDispatchableForDeviceAsync(
        Guid deviceId, DateTime staleDispatchBefore, CancellationToken cancellationToken = default);

    /// <summary>Tracked command by id, for recording a result.</summary>
    Task<DeviceCommand?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// True while a device has an app command still in flight: Pending, or
    /// Dispatched more recently than <paramref name="staleDispatchBefore"/>.
    /// A stale Dispatched command does not count, so it can be re-queued.
    /// </summary>
    Task<bool> HasActiveCommandForAppAsync(
        Guid deviceId, string appName, DateTime staleDispatchBefore, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
