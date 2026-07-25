using EMS.Agent.Models;

namespace EMS.Agent.Services;

/// <summary>
/// REST client for the EMS.API server.
/// </summary>
public interface IApiClientService
{
    Task<bool> RegisterDeviceAsync(DeviceInventoryModel inventory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a liveness heartbeat using the stored device token. Returns a
    /// failed outcome (without calling the server) when the agent has not
    /// registered yet.
    /// </summary>
    Task<HeartbeatOutcome> SendHeartbeatAsync(HeartbeatModel heartbeat, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a batch of per-application usage deltas. Returns false (without
    /// calling the server) when the agent has not registered yet.
    /// </summary>
    Task<bool> SendAppUsageAsync(IReadOnlyList<AppUsageModel> usage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports the full installed-application inventory. Returns false
    /// (without calling the server) when the agent has not registered yet.
    /// </summary>
    Task<bool> SendInstalledAppsAsync(
        IReadOnlyList<InstalledAppModel> applications, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a heartbeat attempt. The policy fields are only meaningful when
/// <see cref="Success"/> is true.
/// </summary>
public sealed record HeartbeatOutcome(
    bool Success,
    bool UsbBlockingEnabled,
    bool StoreGatingEnabled,
    IReadOnlyList<string> BlockedWebsites)
{
    public static readonly HeartbeatOutcome Failed =
        new(false, false, false, Array.Empty<string>());
}
