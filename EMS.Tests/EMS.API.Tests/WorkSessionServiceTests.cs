using EMS.API.DTOs;
using EMS.API.Entities;
using EMS.API.Repositories;
using EMS.API.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace EMS.API.Tests;

public class WorkSessionServiceTests
{
    private const string DeviceIdString = "DEVICE-001";
    private static readonly Guid DeviceInternalId = Guid.NewGuid();
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static WorkSessionService Create(FakeWorkRepo work, FakeDeviceRepo devices)
        => new(work, devices, NullLogger<WorkSessionService>.Instance);

    private static FakeDeviceRepo DeviceRepo() => new(new Device
    {
        Id = DeviceInternalId, DeviceId = DeviceIdString, DeviceName = "T", SerialNumber = "SN"
    });

    [Fact]
    public async Task Record_FirstReport_CreatesDayRow()
    {
        var work = new FakeWorkRepo();
        var service = Create(work, DeviceRepo());

        var ok = await service.RecordAsync(DeviceIdString, new WorkTimeReportRequest
        {
            Sessions = new() { new WorkTimeDelta { WorkDate = Today, SecondsDelta = 120 } }
        });

        Assert.True(ok);
        Assert.Equal(120, Assert.Single(work.Items).WorkedSeconds);
    }

    [Fact]
    public async Task Record_SecondReport_IncrementsSameDay()
    {
        var work = new FakeWorkRepo();
        work.Items.Add(new WorkSessionRecord
        {
            Id = Guid.NewGuid(), DeviceId = DeviceInternalId, WorkDate = Today, WorkedSeconds = 100
        });
        var service = Create(work, DeviceRepo());

        await service.RecordAsync(DeviceIdString, new WorkTimeReportRequest
        {
            Sessions = new() { new WorkTimeDelta { WorkDate = Today, SecondsDelta = 60 } }
        });

        Assert.Equal(160, Assert.Single(work.Items).WorkedSeconds);
    }

    [Fact]
    public async Task Record_UnknownDevice_ReturnsFalse()
    {
        var service = Create(new FakeWorkRepo(), DeviceRepo());
        var ok = await service.RecordAsync("NOPE", new WorkTimeReportRequest());
        Assert.False(ok);
    }

    [Fact]
    public async Task SetPowerState_Suspended_StampsSuspendedAt()
    {
        var devices = DeviceRepo();
        var service = Create(new FakeWorkRepo(), devices);

        var ok = await service.SetPowerStateAsync(DeviceIdString, suspended: true);

        Assert.True(ok);
        Assert.NotNull(devices.Device.SuspendedAt);
    }

    private sealed class FakeWorkRepo : IWorkSessionRepository
    {
        public List<WorkSessionRecord> Items { get; } = new();

        public Task<WorkSessionRecord?> GetTrackedAsync(Guid deviceId, DateOnly workDate, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(w => w.DeviceId == deviceId && w.WorkDate == workDate));

        public Task AddAsync(WorkSessionRecord record, CancellationToken ct = default)
        {
            Items.Add(record);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WorkSessionRecord>> GetByDeviceSinceAsync(
            Guid deviceId, DateOnly fromDate, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WorkSessionRecord>>(
                Items.Where(w => w.DeviceId == deviceId && w.WorkDate >= fromDate).ToList());

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeDeviceRepo : IDeviceRepository
    {
        public Device Device { get; }
        public FakeDeviceRepo(Device device) => Device = device;

        public Task<Device?> GetByDeviceIdAsync(string deviceId, CancellationToken ct = default)
            => Task.FromResult(deviceId == Device.DeviceId ? Device : null);
        public Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(id == Device.Id ? Device : null);
        public Task<Device?> GetTrackedByIdAsync(Guid id, CancellationToken ct = default) => GetByIdAsync(id, ct);
        public Task<IReadOnlyList<Device>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Device>>(new[] { Device });
        public Task AddAsync(Device device, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
