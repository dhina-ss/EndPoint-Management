using EMS.Agent.Models;

namespace EMS.Agent.Services;

/// <summary>
/// REST client for the EMS.API server.
/// </summary>
public interface IApiClientService
{
    Task<bool> RegisterDeviceAsync(DeviceInventoryModel inventory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a liveness heartbeat using the stored device token. Returns false
    /// (without calling the server) when the agent has not registered yet.
    /// </summary>
    Task<bool> SendHeartbeatAsync(HeartbeatModel heartbeat, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a batch of per-application usage deltas. Returns false (without
    /// calling the server) when the agent has not registered yet.
    /// </summary>
    Task<bool> SendAppUsageAsync(IReadOnlyList<AppUsageModel> usage, CancellationToken cancellationToken = default);
}
