using System.Runtime.Versioning;
using EMS.Agent.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace EMS.Agent.Tests;

[SupportedOSPlatform("windows")]
public class StoreUnlockStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly string _filePath;

    public StoreUnlockStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ems-store-unlock", Guid.NewGuid().ToString("N"));
        _filePath = Path.Combine(_directory, "store-unlock.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private StoreUnlockStore CreateStore()
        => new(NullLogger<StoreUnlockStore>.Instance, _filePath);

    [Fact]
    public void IsUnlockActive_NoFile_ReturnsFalse()
    {
        Assert.False(CreateStore().IsUnlockActive());
    }

    [Fact]
    public void GrantUnlock_ThenActive_AcrossInstances()
    {
        CreateStore().GrantUnlock(TimeSpan.FromMinutes(15));

        // A separate instance simulates the SYSTEM service reading what the
        // per-user unlock window wrote.
        Assert.True(CreateStore().IsUnlockActive());
    }

    [Fact]
    public void IsUnlockActive_ExpiredUnlock_ReturnsFalse()
    {
        // A window that already elapsed must not count as active.
        CreateStore().GrantUnlock(TimeSpan.FromSeconds(-1));

        Assert.False(CreateStore().IsUnlockActive());
    }

    [Fact]
    public void IsUnlockActive_CorruptFile_ReturnsFalse()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_filePath, "{ not json");

        Assert.False(CreateStore().IsUnlockActive());
    }
}
