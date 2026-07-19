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
}
