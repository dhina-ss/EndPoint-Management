using EMS.API.DTOs;

namespace EMS.API.Services;

public interface IHeartbeatService
{
    /// <summary>
    /// Records a heartbeat for the device identified by the (already
    /// token-validated) external device id. Returns null when the device
    /// does not exist.
    /// </summary>
    Task<HeartbeatResponse?> RecordHeartbeatAsync(
        string deviceId, HeartbeatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Latest live-monitoring snapshot for a device. Returns null when the
    /// device does not exist; returns an empty snapshot (IsOnline=false) when
    /// it exists but has never reported.
    /// </summary>
    Task<DeviceMetricsResponse?> GetLatestMetricsAsync(
        Guid deviceInternalId, CancellationToken cancellationToken = default);
}
