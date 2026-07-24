using EMS.API.Entities;

namespace EMS.API.Repositories;

/// <summary>
/// Storage for the installed-application inventory and the per-device block
/// list. Both are small, device-scoped sets, so they live behind one
/// repository.
/// </summary>
public interface IApplicationInventoryRepository
{
    Task<IReadOnlyList<InstalledApplication>> GetInstalledAsync(
        Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>Replaces the device's whole inventory with a fresh scan.</summary>
    Task ReplaceInstalledAsync(
        Guid deviceId, IEnumerable<InstalledApplication> applications, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BlockedApplication>> GetBlockedAsync(
        Guid deviceId, CancellationToken cancellationToken = default);

    Task<BlockedApplication?> GetBlockedByExecutableAsync(
        Guid deviceId, string executableName, CancellationToken cancellationToken = default);

    Task<BlockedApplication?> GetBlockedByIdAsync(
        Guid deviceId, Guid blockId, CancellationToken cancellationToken = default);

    Task AddBlockedAsync(BlockedApplication blocked, CancellationToken cancellationToken = default);

    void RemoveBlocked(BlockedApplication blocked);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
