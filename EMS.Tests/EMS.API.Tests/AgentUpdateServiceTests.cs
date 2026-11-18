using System.Text;
using EMS.API.Configuration;
using EMS.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EMS.API.Tests;

public class AgentUpdateServiceTests
{
    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "EMS.API";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
    }

    [Fact]
    public void GetUpdateInfo_ReturnsConfiguredSettings()
    {
        var settings = Options.Create(new AgentUpdateSettings
        {
            Enabled = true,
            LatestVersion = "1.0.30",
            DownloadUrl = "/api/agent/update/download",
            Sha256 = "abc123sha",
            InstallerPath = "nonexistent.exe"
        });

        var env = new FakeWebHostEnvironment();
        var service = new AgentUpdateService(settings, env, NullLogger<AgentUpdateService>.Instance);

        var info = service.GetUpdateInfo();

        Assert.True(info.Enabled);
        Assert.Equal("1.0.30", info.LatestVersion);
        Assert.Equal("/api/agent/update/download", info.DownloadUrl);
        Assert.Equal("abc123sha", info.Sha256);
        Assert.False(info.HasInstallerFile);
    }

    [Fact]
    public async Task RegisterUpdatePackageAsync_SavesFileAndComputesSha256()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ems-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var settings = Options.Create(new AgentUpdateSettings
            {
                Enabled = true,
                InstallerPath = Path.Combine(tempDir, "TheLanceEMSSetup.exe")
            });

            var env = new FakeWebHostEnvironment { ContentRootPath = tempDir };
            var service = new AgentUpdateService(settings, env, NullLogger<AgentUpdateService>.Instance);

            var sampleContent = Encoding.UTF8.GetBytes("Simulated installer content for testing");
            using var stream = new MemoryStream(sampleContent);

            var updateInfo = await service.RegisterUpdatePackageAsync("1.0.31", stream);

            Assert.True(updateInfo.Enabled);
            Assert.Equal("1.0.31", updateInfo.LatestVersion);
            Assert.False(string.IsNullOrWhiteSpace(updateInfo.Sha256));
            Assert.True(updateInfo.HasInstallerFile);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
