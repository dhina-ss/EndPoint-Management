using EMS.API.Entities;

namespace EMS.API.Repositories;

/// <summary>Storage for per-device daily network data usage.</summary>
public interface INetworkUsageRepository
{
    /// <summary>Tracked row for a device/day, or null (for the upsert).</summary>
    Task<NetworkUsageRecord?> GetTrackedAsync(
        Guid deviceId, DateOnly usageDate, CancellationToken cancellationToken = default);

    Task AddAsync(NetworkUsageRecord record, CancellationToken cancellationToken = default);

    /// <summary>Daily usage for a device from <paramref name="fromDate"/> onward, newest first.</summary>
    Task<IReadOnlyList<NetworkUsageRecord>> GetByDeviceSinceAsync(
        Guid deviceId, DateOnly fromDate, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
