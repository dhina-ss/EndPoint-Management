using System.Runtime.Versioning;
using System.Text.Json;
using EMS.Agent.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace EMS.Agent.Tests;

[SupportedOSPlatform("windows")]
public class DeviceIdServiceTests : IDisposable
{
    private readonly string _directory;
    private readonly string _filePath;

    public DeviceIdServiceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ems-agent-tests", Guid.NewGuid().ToString("N"));
        _filePath = Path.Combine(_directory, "device-id.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private DeviceIdService CreateService()
        => new(NullLogger<DeviceIdService>.Instance, _filePath);

    [Fact]
    public async Task GetDeviceIdAsync_FirstRun_GeneratesGuidAndPersistsFile()
    {
        using var service = CreateService();

        var deviceId = await service.GetDeviceIdAsync();

        Assert.True(Guid.TryParse(deviceId, out _));
        Assert.True(File.Exists(_filePath));

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(_filePath));
        Assert.Equal(deviceId, document.RootElement.GetProperty("DeviceId").GetString());
    }

    [Fact]
    public async Task GetDeviceIdAsync_NextRun_ReadsExistingId()
    {
        string firstRunId;
        using (var firstRun = CreateService())
        {
            firstRunId = await firstRun.GetDeviceIdAsync();
        }

        // A new instance simulates a service restart.
        using var secondRun = CreateService();
        var secondRunId = await secondRun.GetDeviceIdAsync();

        Assert.Equal(firstRunId, secondRunId);
    }

    [Fact]
    public async Task GetDeviceIdAsync_ConcurrentCalls_ReturnSameId()
    {
        using var service = CreateService();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => service.GetDeviceIdAsync()));

        Assert.Single(results.Distinct());
    }

    [Fact]
    public async Task GetDeviceIdAsync_CorruptFile_RegeneratesId()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(_filePath, "{ not valid json");

        using var service = CreateService();
        var deviceId = await service.GetDeviceIdAsync();

        Assert.True(Guid.TryParse(deviceId, out _));
    }
}
