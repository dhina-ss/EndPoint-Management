using EMS.API.DTOs;

namespace EMS.API.Services;

public interface ISecurityStatusService
{
    /// <summary>Stores a device's Defender status and replaces its threat set.</summary>
    Task<bool> RecordAsync(
        string deviceId, SecurityStatusReportRequest request, CancellationToken cancellationToken = default);

    /// <summary>Threat history for the dashboard; null if the device is unknown.</summary>
    Task<IReadOnlyList<ThreatResponse>?> GetThreatsAsync(
        Guid deviceInternalId, CancellationToken cancellationToken = default);
}
