using EMS.Agent.Models;

namespace EMS.Agent.Services;

/// <summary>
/// REST client for the EMS.API server.
/// </summary>
public interface IApiClientService
{
    Task<bool> RegisterDeviceAsync(DeviceInventoryModel inventory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells the server this device is being uninstalled so it removes the
    /// device record. Best-effort; used from the uninstaller.
    /// </summary>
    Task<bool> ReportUninstallAsync(CancellationToken cancellationToken = default);

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

    /// <summary>Sends per-day working-time deltas. Returns false if not registered.</summary>
    Task<bool> SendWorkTimeAsync(IReadOnlyList<WorkTimeModel> sessions, CancellationToken cancellationToken = default);

    /// <summary>Reports the Microsoft Defender status + threat snapshot. Returns false if not registered.</summary>
    Task<bool> SendSecurityStatusAsync(SecurityStatusModel status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Best-effort beacon that the device is suspending (for the sleep status).
    /// Fire-and-forget; failures are ignored.
    /// </summary>
    Task SendPowerStateAsync(bool suspended, CancellationToken cancellationToken = default);

    /// <summary>Best-effort beacon that the workstation was auto-locked after inactivity.</summary>
    Task SendIdleLockAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Best-effort beacon that the device stayed locked past the alert window; the
    /// server emails the assigned manager. Fire-and-forget.
    /// </summary>
    Task SendIdleAlertAsync(CancellationToken cancellationToken = default);

    /// <summary>Reports a precise GPS location fix. Best-effort.</summary>
    Task SendLocationAsync(double latitude, double longitude, double accuracyMeters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports the full installed-application inventory. Returns false
    /// (without calling the server) when the agent has not registered yet.
    /// </summary>
    Task<bool> SendInstalledAppsAsync(
        IReadOnlyList<InstalledAppModel> applications, CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls for pending software-management commands. Each returned command is
    /// marked Dispatched server-side. Returns an empty list when there is
    /// nothing to do or the agent has not registered yet.
    /// </summary>
    Task<IReadOnlyList<PendingCommandModel>> GetPendingCommandsAsync(CancellationToken cancellationToken = default);

    /// <summary>Reports the outcome of a command back to the server.</summary>
    Task<bool> ReportCommandResultAsync(
        Guid commandId, CommandResultModel result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the current server-side status of a command (e.g. "Dispatched",
    /// "Cancelled"). Used to abort a running install promptly when the console
    /// cancels it. Returns null if the status could not be read.
    /// </summary>
    Task<string?> GetCommandStatusAsync(Guid commandId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads an installer package to a temp file, verifying its SHA-256.
    /// Returns the temp file path, or null on failure. The caller deletes it.
    /// </summary>
    Task<string?> DownloadPackageAsync(
        Guid packageId, string? expectedSha256, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a heartbeat attempt. The policy fields are only meaningful when
/// <see cref="Success"/> is true.
/// </summary>
public sealed record HeartbeatOutcome(
    bool Success,
    bool UsbBlockingEnabled,
    bool StoreGatingEnabled,
    IReadOnlyList<string> BlockedWebsites,
    bool RequireLoginEachStartup,
    int InactivityLockSeconds,
    int InactivityAlertSeconds)
{
    public static readonly HeartbeatOutcome Failed =
        new(false, false, false, Array.Empty<string>(), false, 0, 0);
}
