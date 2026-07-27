using EMS.API.DTOs;
using EMS.API.Entities;
using EMS.API.Repositories;

namespace EMS.API.Services;

public class DeviceCommandService : IDeviceCommandService
{
    private const int HistoryLimit = 50;

    private readonly IDeviceCommandRepository _commands;
    private readonly IDeviceRepository _devices;
    private readonly IApplicationInventoryRepository _inventory;
    private readonly IInstallerPackageRepository _packages;
    private readonly ILogger<DeviceCommandService> _logger;

    public DeviceCommandService(
        IDeviceCommandRepository commands,
        IDeviceRepository devices,
        IApplicationInventoryRepository inventory,
        IInstallerPackageRepository packages,
        ILogger<DeviceCommandService> logger)
    {
        _commands = commands;
        _devices = devices;
        _inventory = inventory;
        _packages = packages;
        _logger = logger;
    }

    public async Task<EnqueueCommandResult> EnqueueUninstallAsync(
        Guid deviceInternalId, Guid installedAppId, CancellationToken cancellationToken = default)
    {
        var device = await _devices.GetByIdAsync(deviceInternalId, cancellationToken);
        if (device is null)
        {
            return new EnqueueCommandResult(EnqueueCommandOutcome.DeviceNotFound, null, "Device not found.");
        }

        var app = await _inventory.GetByIdAsync(installedAppId, cancellationToken);
        if (app is null || app.DeviceId != deviceInternalId)
        {
            return new EnqueueCommandResult(EnqueueCommandOutcome.AppNotFound, null, "Application not found on this device.");
        }

        // Don't stack duplicate uninstalls for the same app while one is in flight.
        if (await _commands.HasActiveCommandForAppAsync(deviceInternalId, app.Name, cancellationToken))
        {
            return new EnqueueCommandResult(
                EnqueueCommandOutcome.Duplicate, null, $"A command for '{app.Name}' is already in progress.");
        }

        var command = new DeviceCommand
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceInternalId,
            Type = DeviceCommandType.Uninstall,
            Status = DeviceCommandStatus.Pending,
            TargetAppName = app.Name,
            TargetAppVersion = app.Version,
            TargetIsStoreApp = app.IsStoreApp,
            CreatedAt = DateTime.UtcNow
        };

        await _commands.AddAsync(command, cancellationToken);
        await _commands.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Queued uninstall of '{App}' for device {DeviceId}.", app.Name, deviceInternalId);

        return new EnqueueCommandResult(EnqueueCommandOutcome.Created, ToResponse(command, app.Name), null);
    }

    public async Task<EnqueueCommandResult> EnqueueInstallAsync(
        Guid deviceInternalId, Guid packageId, DeviceCommandType type,
        CancellationToken cancellationToken = default)
    {
        var device = await _devices.GetByIdAsync(deviceInternalId, cancellationToken);
        if (device is null)
        {
            return new EnqueueCommandResult(EnqueueCommandOutcome.DeviceNotFound, null, "Device not found.");
        }

        var package = await _packages.GetMetadataByIdAsync(packageId, cancellationToken);
        if (package is null)
        {
            return new EnqueueCommandResult(EnqueueCommandOutcome.PackageNotFound, null, "Installer package not found.");
        }

        var command = new DeviceCommand
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceInternalId,
            Type = type,
            Status = DeviceCommandStatus.Pending,
            PackageId = package.Id,
            CreatedAt = DateTime.UtcNow
        };

        await _commands.AddAsync(command, cancellationToken);
        await _commands.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Queued {Type} of package '{Package}' for device {DeviceId}.",
            type, package.DisplayName, deviceInternalId);

        return new EnqueueCommandResult(EnqueueCommandOutcome.Created, ToResponse(command, package.DisplayName), null);
    }

    public async Task<IReadOnlyList<DeviceCommandResponse>?> GetForDeviceAsync(
        Guid deviceInternalId, CancellationToken cancellationToken = default)
    {
        var device = await _devices.GetByIdAsync(deviceInternalId, cancellationToken);
        if (device is null)
        {
            return null;
        }

        var commands = await _commands.GetForDeviceAsync(deviceInternalId, HistoryLimit, cancellationToken);
        return commands.Select(c => ToResponse(c, c.Package?.DisplayName)).ToList();
    }

    public async Task<IReadOnlyList<PendingCommandDto>?> DispatchPendingAsync(
        string deviceId, CancellationToken cancellationToken = default)
    {
        var device = await _devices.GetByDeviceIdAsync(deviceId, cancellationToken);
        if (device is null)
        {
            return null;
        }

        var pending = await _commands.GetPendingForDeviceAsync(device.Id, cancellationToken);
        if (pending.Count == 0)
        {
            return Array.Empty<PendingCommandDto>();
        }

        var utcNow = DateTime.UtcNow;
        var dtos = new List<PendingCommandDto>(pending.Count);

        foreach (var command in pending)
        {
            // Flip to Dispatched on a tracked copy so a second poll won't re-hand it.
            var tracked = await _commands.GetTrackedByIdAsync(command.Id, cancellationToken);
            if (tracked is null || tracked.Status != DeviceCommandStatus.Pending)
            {
                continue;
            }

            tracked.Status = DeviceCommandStatus.Dispatched;
            tracked.DispatchedAt = utcNow;

            dtos.Add(new PendingCommandDto
            {
                Id = command.Id,
                Type = command.Type.ToString(),
                TargetAppName = command.TargetAppName,
                TargetAppVersion = command.TargetAppVersion,
                TargetIsStoreApp = command.TargetIsStoreApp,
                PackageId = command.PackageId,
                PackageKind = command.Package?.Kind.ToString(),
                SilentArgs = command.Package?.SilentArgs,
                Sha256 = command.Package?.Sha256
            });
        }

        await _commands.SaveChangesAsync(cancellationToken);
        return dtos;
    }

    public async Task<bool> RecordResultAsync(
        string deviceId, Guid commandId, CommandResultRequest result,
        CancellationToken cancellationToken = default)
    {
        var device = await _devices.GetByDeviceIdAsync(deviceId, cancellationToken);
        if (device is null)
        {
            return false;
        }

        var command = await _commands.GetTrackedByIdAsync(commandId, cancellationToken);
        if (command is null || command.DeviceId != device.Id)
        {
            return false;
        }

        command.Status = result.Success ? DeviceCommandStatus.Succeeded : DeviceCommandStatus.Failed;
        command.ResultCode = result.ResultCode;
        command.ResultMessage = result.Message;
        command.CompletedAt = DateTime.UtcNow;

        await _commands.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Command {CommandId} for device {DeviceId} completed: {Status} ({Code}).",
            commandId, deviceId, command.Status, result.ResultCode);

        return true;
    }

    private static DeviceCommandResponse ToResponse(DeviceCommand c, string? packageName) => new()
    {
        Id = c.Id,
        Type = c.Type.ToString(),
        Status = c.Status.ToString(),
        TargetAppName = c.TargetAppName,
        TargetAppVersion = c.TargetAppVersion,
        PackageName = packageName,
        ResultMessage = c.ResultMessage,
        ResultCode = c.ResultCode,
        CreatedAt = c.CreatedAt,
        DispatchedAt = c.DispatchedAt,
        CompletedAt = c.CompletedAt
    };
}
