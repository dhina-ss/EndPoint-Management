using EMS.API.DTOs;

namespace EMS.API.Services;

public interface IApplicationInventoryService
{
    /// <summary>
    /// Stores a fresh inventory scan for the device identified by the
    /// (already token-validated) external device id. Returns false when the
    /// device does not exist.
    /// </summary>
    Task<bool> ReplaceInventoryAsync(
        string deviceId, InstalledAppsReportRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Installed applications for a device, each flagged with whether it is
    /// currently blocked. Returns null when the device does not exist.
    /// </summary>
    Task<IReadOnlyList<InstalledAppResponse>?> GetInventoryAsync(
        Guid deviceInternalId, CancellationToken cancellationToken = default);

    /// <summary>Blocks an application. Returns null when the device does not exist.</summary>
    Task<InstalledAppResponse?> BlockAsync(
        Guid deviceInternalId, BlockApplicationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Unblocks by executable name. Returns false when no such block exists.</summary>
    Task<bool> UnblockAsync(
        Guid deviceInternalId, string executableName, CancellationToken cancellationToken = default);
}
