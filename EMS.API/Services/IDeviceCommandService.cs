using EMS.API.DTOs;
using EMS.API.Entities;

namespace EMS.API.Services;

public enum EnqueueCommandOutcome
{
    Created,
    DeviceNotFound,
    AppNotFound,
    PackageNotFound,
    Duplicate
}

public sealed record EnqueueCommandResult(
    EnqueueCommandOutcome Outcome, DeviceCommandResponse? Command, string? Error);

public interface IDeviceCommandService
{
    /// <summary>Queues a silent uninstall of an inventory app for a device.</summary>
    Task<EnqueueCommandResult> EnqueueUninstallAsync(
        Guid deviceInternalId, Guid installedAppId, CancellationToken cancellationToken = default);

    /// <summary>Queues an Install or Update that runs an uploaded package on a device.</summary>
    Task<EnqueueCommandResult> EnqueueInstallAsync(
        Guid deviceInternalId, Guid packageId, DeviceCommandType type,
        CancellationToken cancellationToken = default);

    /// <summary>Recent commands for a device (for the dashboard).</summary>
    Task<IReadOnlyList<DeviceCommandResponse>?> GetForDeviceAsync(
        Guid deviceInternalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pending commands for the calling agent's device, marking them Dispatched.
    /// Returns null when the device id is unknown.
    /// </summary>
    Task<IReadOnlyList<PendingCommandDto>?> DispatchPendingAsync(
        string deviceId, CancellationToken cancellationToken = default);

    /// <summary>Records the agent's result for a command it was handed.</summary>
    Task<bool> RecordResultAsync(
        string deviceId, Guid commandId, CommandResultRequest result,
        CancellationToken cancellationToken = default);
}
