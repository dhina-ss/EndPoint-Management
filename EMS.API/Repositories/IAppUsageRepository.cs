using EMS.API.Entities;

namespace EMS.API.Repositories;

public interface IAppUsageRepository
{
    /// <summary>Tracked lookup for the upsert path (report ingestion).</summary>
    Task<AppUsageRecord?> GetTrackedAsync(
        Guid deviceId, string applicationName, DateOnly usageDate, CancellationToken cancellationToken = default);

    Task AddAsync(AppUsageRecord record, CancellationToken cancellationToken = default);

    /// <summary>Read-only lookup for the dashboard summary query.</summary>
    Task<IReadOnlyList<AppUsageRecord>> GetByDeviceAndDateAsync(
        Guid deviceId, DateOnly usageDate, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
