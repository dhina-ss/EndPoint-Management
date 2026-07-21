using EMS.API.DTOs;

namespace EMS.API.Services;

public interface IAppUsageService
{
    /// <summary>
    /// Applies a batch of usage deltas for the device identified by the
    /// (already token-validated) external device id. Returns null when the
    /// device does not exist.
    /// </summary>
    Task<AppUsageReportResponse?> RecordUsageAsync(
        string deviceId, AppUsageReportRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppUsageSummaryResponse>> GetUsageAsync(
        Guid deviceInternalId, DateOnly usageDate, CancellationToken cancellationToken = default);
}
