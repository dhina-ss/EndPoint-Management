using EMS.API.Entities;
using EMS.API.Repositories;
using EMS.API.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace EMS.API.Tests;

public class DeviceCommandServiceTests
{
    private static readonly Guid DeviceInternalId = Guid.NewGuid();
    private const string DeviceIdString = "DEVICE-001";

    private static DeviceCommandService CreateService(
        FakeCommandRepo commands, FakeDeviceRepo devices, FakeInventoryRepo inventory, FakePackageRepo packages)
        => new(commands, devices, inventory, packages, NullLogger<DeviceCommandService>.Instance);

    [Fact]
    public async Task EnqueueUninstall_KnownApp_CreatesPendingCommand()
    {
        var app = new InstalledApplication
        {
            Id = Guid.NewGuid(), DeviceId = DeviceInternalId, Name = "7-Zip", Version = "24.08", IsStoreApp = false
        };
        var commands = new FakeCommandRepo();
        var service = CreateService(commands, DeviceRepo(), new FakeInventoryRepo(app), new FakePackageRepo());

        var result = await service.EnqueueUninstallAsync(DeviceInternalId, app.Id);

        Assert.Equal(EnqueueCommandOutcome.Created, result.Outcome);
        var stored = Assert.Single(commands.Items);
        Assert.Equal(DeviceCommandType.Uninstall, stored.Type);
        Assert.Equal(DeviceCommandStatus.Pending, stored.Status);
        Assert.Equal("7-Zip", stored.TargetAppName);
    }

    [Fact]
    public async Task EnqueueUninstall_UnknownApp_ReturnsAppNotFound()
    {
        var service = CreateService(new FakeCommandRepo(), DeviceRepo(), new FakeInventoryRepo(), new FakePackageRepo());

        var result = await service.EnqueueUninstallAsync(DeviceInternalId, Guid.NewGuid());

        Assert.Equal(EnqueueCommandOutcome.AppNotFound, result.Outcome);
    }

    [Fact]
    public async Task EnqueueUninstall_WhenAlreadyInFlight_ReturnsDuplicate()
    {
        var app = new InstalledApplication { Id = Guid.NewGuid(), DeviceId = DeviceInternalId, Name = "7-Zip" };
        var commands = new FakeCommandRepo();
        commands.Items.Add(new DeviceCommand
        {
            Id = Guid.NewGuid(), DeviceId = DeviceInternalId, TargetAppName = "7-Zip",
            Type = DeviceCommandType.Uninstall, Status = DeviceCommandStatus.Dispatched
        });
        var service = CreateService(commands, DeviceRepo(), new FakeInventoryRepo(app), new FakePackageRepo());

        var result = await service.EnqueueUninstallAsync(DeviceInternalId, app.Id);

        Assert.Equal(EnqueueCommandOutcome.Duplicate, result.Outcome);
    }

    [Fact]
    public async Task DispatchPending_MarksCommandsDispatched()
    {
        var commands = new FakeCommandRepo();
        commands.Items.Add(new DeviceCommand
        {
            Id = Guid.NewGuid(), DeviceId = DeviceInternalId, TargetAppName = "7-Zip",
            Type = DeviceCommandType.Uninstall, Status = DeviceCommandStatus.Pending
        });
        var service = CreateService(commands, DeviceRepo(), new FakeInventoryRepo(), new FakePackageRepo());

        var dispatched = await service.DispatchPendingAsync(DeviceIdString);

        Assert.NotNull(dispatched);
        Assert.Single(dispatched!);
        Assert.Equal(DeviceCommandStatus.Dispatched, commands.Items[0].Status);
        Assert.NotNull(commands.Items[0].DispatchedAt);
    }

    [Fact]
    public async Task RecordResult_Failure_SetsFailedStatusAndMessage()
    {
        var command = new DeviceCommand
        {
            Id = Guid.NewGuid(), DeviceId = DeviceInternalId, Status = DeviceCommandStatus.Dispatched
        };
        var commands = new FakeCommandRepo();
        commands.Items.Add(command);
        var service = CreateService(commands, DeviceRepo(), new FakeInventoryRepo(), new FakePackageRepo());

        var ok = await service.RecordResultAsync(
            DeviceIdString, command.Id, new EMS.API.DTOs.CommandResultRequest
            {
                Success = false, ResultCode = 1603, Message = "MSI failed"
            });

        Assert.True(ok);
        Assert.Equal(DeviceCommandStatus.Failed, command.Status);
        Assert.Equal(1603, command.ResultCode);
        Assert.Equal("MSI failed", command.ResultMessage);
        Assert.NotNull(command.CompletedAt);
    }

    [Fact]
    public async Task RecordResult_ForOtherDevicesCommand_IsRejected()
    {
        var command = new DeviceCommand { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid() };
        var commands = new FakeCommandRepo();
        commands.Items.Add(command);
        var service = CreateService(commands, DeviceRepo(), new FakeInventoryRepo(), new FakePackageRepo());

        var ok = await service.RecordResultAsync(
            DeviceIdString, command.Id, new EMS.API.DTOs.CommandResultRequest { Success = true });

        Assert.False(ok);
    }

    private static FakeDeviceRepo DeviceRepo() => new(new Device
    {
        Id = DeviceInternalId, DeviceId = DeviceIdString, DeviceName = "Test", SerialNumber = "SN"
    });

    // ---- Dependency-free fakes ----

    private sealed class FakeCommandRepo : IDeviceCommandRepository
    {
        public List<DeviceCommand> Items { get; } = new();

        public Task AddAsync(DeviceCommand command, CancellationToken ct = default)
        {
            Items.Add(command);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DeviceCommand>> GetForDeviceAsync(Guid deviceId, int limit, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DeviceCommand>>(
                Items.Where(c => c.DeviceId == deviceId).Take(limit).ToList());

        public Task<IReadOnlyList<DeviceCommand>> GetPendingForDeviceAsync(Guid deviceId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DeviceCommand>>(
                Items.Where(c => c.DeviceId == deviceId && c.Status == DeviceCommandStatus.Pending).ToList());

        public Task<DeviceCommand?> GetTrackedByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(c => c.Id == id));

        public Task<bool> HasActiveCommandForAppAsync(Guid deviceId, string appName, CancellationToken ct = default)
            => Task.FromResult(Items.Any(c => c.DeviceId == deviceId && c.TargetAppName == appName
                && (c.Status == DeviceCommandStatus.Pending || c.Status == DeviceCommandStatus.Dispatched)));

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeDeviceRepo : IDeviceRepository
    {
        private readonly Device _device;
        public FakeDeviceRepo(Device device) => _device = device;

        public Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(id == _device.Id ? _device : null);

        public Task<Device?> GetByDeviceIdAsync(string deviceId, CancellationToken ct = default)
            => Task.FromResult(deviceId == _device.DeviceId ? _device : null);

        public Task<Device?> GetTrackedByIdAsync(Guid id, CancellationToken ct = default) => GetByIdAsync(id, ct);
        public Task<IReadOnlyList<Device>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Device>>(new[] { _device });
        public Task AddAsync(Device device, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeInventoryRepo : IApplicationInventoryRepository
    {
        private readonly InstalledApplication? _app;
        public FakeInventoryRepo(InstalledApplication? app = null) => _app = app;

        public Task<InstalledApplication?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_app is not null && _app.Id == id ? _app : null);

        public Task<IReadOnlyList<InstalledApplication>> GetInstalledAsync(Guid deviceId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<InstalledApplication>>(Array.Empty<InstalledApplication>());
        public Task ReplaceInstalledAsync(Guid deviceId, IEnumerable<InstalledApplication> apps, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakePackageRepo : IInstallerPackageRepository
    {
        public Task<InstallerPackage?> GetMetadataByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<InstallerPackage?>(new InstallerPackage { Id = id, DisplayName = "Pkg" });

        public Task AddAsync(InstallerPackage package, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<InstallerPackage>> GetAllMetadataAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<InstallerPackage>>(Array.Empty<InstallerPackage>());
        public Task<InstallerPackage?> GetWithContentAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<InstallerPackage?>(null);
        public Task<bool> IsReferencedByCommandAsync(Guid id, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => Task.FromResult(true);
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
