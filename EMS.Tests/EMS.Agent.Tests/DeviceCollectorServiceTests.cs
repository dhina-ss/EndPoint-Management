using System.Runtime.Versioning;
using EMS.Agent.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace EMS.Agent.Tests;

[SupportedOSPlatform("windows")]
public class DeviceCollectorServiceTests
{
    private const string TestDeviceId = "11111111-2222-3333-4444-555555555555";

    private static DeviceCollectorService CreateService()
    {
        return new DeviceCollectorService(
            new FixedDeviceIdService(TestDeviceId),
            NullLogger<DeviceCollectorService>.Instance);
    }

    [Fact]
    public async Task CollectAsync_ReturnsRegistrableInventory()
    {
        var inventory = await CreateService().CollectAsync();

        // The API contract requires these three fields.
        Assert.Equal(TestDeviceId, inventory.DeviceId);
        Assert.False(string.IsNullOrWhiteSpace(inventory.DeviceName));
        Assert.False(string.IsNullOrWhiteSpace(inventory.SerialNumber));
    }

    [Fact]
    public async Task CollectAsync_CollectsHardwareAndOsDetails()
    {
        var inventory = await CreateService().CollectAsync();

        Assert.False(string.IsNullOrWhiteSpace(inventory.Processor));
        Assert.False(string.IsNullOrWhiteSpace(inventory.OSVersion));
        Assert.False(string.IsNullOrWhiteSpace(inventory.OSBuildNumber));
        Assert.False(string.IsNullOrWhiteSpace(inventory.RamSize));
        Assert.False(string.IsNullOrWhiteSpace(inventory.StorageSize));
        Assert.NotNull(inventory.LastBootTime);
    }

    private sealed class FixedDeviceIdService : IDeviceIdService
    {
        private readonly string _deviceId;

        public FixedDeviceIdService(string deviceId) => _deviceId = deviceId;

        public Task<string> GetDeviceIdAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_deviceId);
    }
}
