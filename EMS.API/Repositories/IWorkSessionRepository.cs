using EMS.API.Entities;

namespace EMS.API.Repositories;

/// <summary>Storage for per-device daily working-time totals.</summary>
public interface IWorkSessionRepository
{
    Task<WorkSessionRecord?> GetTrackedAsync(
        Guid deviceId, DateOnly workDate, CancellationToken cancellationToken = default);

    Task AddAsync(WorkSessionRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkSessionRecord>> GetByDeviceSinceAsync(
        Guid deviceId, DateOnly fromDate, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
