using EMS.API.Entities;

namespace EMS.API.Repositories;

/// <summary>Storage for the per-device installed-application inventory.</summary>
public interface IApplicationInventoryRepository
{
    Task<IReadOnlyList<InstalledApplication>> GetInstalledAsync(
        Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>One inventory row by its id, or null if missing.</summary>
    Task<InstalledApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Replaces the device's whole inventory with a fresh scan.</summary>
    Task ReplaceInstalledAsync(
        Guid deviceId, IEnumerable<InstalledApplication> applications, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
