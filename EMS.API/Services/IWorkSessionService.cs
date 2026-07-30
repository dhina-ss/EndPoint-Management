using EMS.API.DTOs;

namespace EMS.API.Services;

public interface IWorkSessionService
{
    /// <summary>Adds reported working-time deltas to each day's running total.</summary>
    Task<bool> RecordAsync(
        string deviceId, WorkTimeReportRequest request, CancellationToken cancellationToken = default);

    /// <summary>Daily working-time totals for a device over the last N days.</summary>
    Task<IReadOnlyList<WorkTimeResponse>?> GetForDeviceAsync(
        Guid deviceInternalId, int days, CancellationToken cancellationToken = default);

    /// <summary>Records a suspend/resume beacon for the device's sleep status.</summary>
    Task<bool> SetPowerStateAsync(
        string deviceId, bool suspended, CancellationToken cancellationToken = default);
}
