using System.Runtime.Versioning;
using System.Text.Json;
using EMS.Agent.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace EMS.Agent.Tests;

[SupportedOSPlatform("windows")]
public class DeviceTokenServiceTests : IDisposable
{
    private const string TestDeviceId = "11111111-2222-3333-4444-555555555555";

    private readonly string _directory;
    private readonly string _filePath;

    public DeviceTokenServiceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ems-agent-tests", Guid.NewGuid().ToString("N"));
        _filePath = Path.Combine(_directory, "device-auth.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private DeviceTokenService CreateService(string deviceId = TestDeviceId)
        => new(new FixedDeviceIdService(deviceId), NullLogger<DeviceTokenService>.Instance, _filePath);

    [Fact]
    public async Task GetTokenAsync_NeverRegistered_ReturnsNull()
    {
        using var service = CreateService();

        Assert.Null(await service.GetTokenAsync());
    }

    [Fact]
    public async Task SaveTokenAsync_PersistsDeviceIdAndToken()
    {
        using var service = CreateService();

        await service.SaveTokenAsync(TestDeviceId, "token-123");

        Assert.Equal("token-123", await service.GetTokenAsync());
        Assert.True(File.Exists(_filePath));

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(_filePath));
        Assert.Equal(TestDeviceId, document.RootElement.GetProperty("DeviceId").GetString());
        Assert.Equal("token-123", document.RootElement.GetProperty("Token").GetString());
    }

    [Fact]
    public async Task GetTokenAsync_AfterRestart_ReadsPersistedToken()
    {
        using (var firstRun = CreateService())
        {
            await firstRun.SaveTokenAsync(TestDeviceId, "token-456");
        }

        // A new instance simulates a service restart.
        using var secondRun = CreateService();

        Assert.Equal("token-456", await secondRun.GetTokenAsync());
    }

    [Fact]
    public async Task GetTokenAsync_TokenForDifferentDevice_IsIgnored()
    {
        using (var firstRun = CreateService())
        {
            await firstRun.SaveTokenAsync(TestDeviceId, "token-789");
        }

        // The device identity changed (device-id.json reset); the stored
        // credential must not be reused.
        using var otherIdentity = CreateService(deviceId: "99999999-8888-7777-6666-555555555555");

        Assert.Null(await otherIdentity.GetTokenAsync());
    }

    [Fact]
    public async Task GetTokenAsync_CorruptFile_ReturnsNull()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(_filePath, "{ not valid json");

        using var service = CreateService();

        Assert.Null(await service.GetTokenAsync());
    }

    private sealed class FixedDeviceIdService : IDeviceIdService
    {
        private readonly string _deviceId;

        public FixedDeviceIdService(string deviceId) => _deviceId = deviceId;

        public Task<string> GetDeviceIdAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_deviceId);
    }
}
