using System.Runtime.Versioning;
using EMS.Agent.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace EMS.Agent.Tests;

[SupportedOSPlatform("windows")]
public class ActivationStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly string _filePath;

    public ActivationStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ems-activation-tests", Guid.NewGuid().ToString("N"));
        _filePath = Path.Combine(_directory, "activation.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private ActivationStore CreateStore()
        => new(NullLogger<ActivationStore>.Instance, _filePath);

    [Fact]
    public void IsActivated_BeforeActivation_ReturnsFalse()
    {
        Assert.False(CreateStore().IsActivated());
    }

    [Fact]
    public void Activate_ThenIsActivated_ReturnsTrueAndRecordsUser()
    {
        CreateStore().Activate("jane.doe");

        // A fresh instance (as the service, a separate process, would see it).
        var reader = CreateStore();
        Assert.True(reader.IsActivated());
        Assert.Equal("jane.doe", reader.ActivatedBy());
    }

    [Fact]
    public void IsActivated_EmptyActivatedBy_ReturnsFalse()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_filePath, """{"ActivatedBy":"","ActivatedAt":"2026-01-01T00:00:00Z"}""");

        Assert.False(CreateStore().IsActivated());
    }

    [Fact]
    public void IsActivated_CorruptFile_ReturnsFalse()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_filePath, "{ not json");

        Assert.False(CreateStore().IsActivated());
    }
}
