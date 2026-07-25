using System.Text.Json;

namespace EMS.Agent.Services;

/// <summary>
/// File-backed unlock state in ProgramData\EMS.Agent\store-unlock.json. Both
/// the per-user unlock window (write) and the SYSTEM service (read) can access
/// ProgramData, so this coordinates them across processes.
/// </summary>
public class StoreUnlockStore : IStoreUnlockStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly ILogger<StoreUnlockStore> _logger;

    public StoreUnlockStore(ILogger<StoreUnlockStore> logger)
        : this(logger, DefaultFilePath())
    {
    }

    /// <summary>Overload for tests; production uses the default path.</summary>
    public StoreUnlockStore(ILogger<StoreUnlockStore> logger, string filePath)
    {
        _logger = logger;
        _filePath = filePath;
    }

    public bool IsUnlockActive()
    {
        var until = ReadUnlockUntil();
        return until is not null && until.Value > DateTime.UtcNow;
    }

    public void GrantUnlock(TimeSpan duration)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            var document = new UnlockDocument { UnlockedUntil = DateTime.UtcNow.Add(duration) };
            File.WriteAllText(_filePath, JsonSerializer.Serialize(document, SerializerOptions));

            _logger.LogInformation("Microsoft Store unlocked until {Until} (UTC).", document.UnlockedUntil);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist the Store unlock to {Path}.", _filePath);
            throw;
        }
    }

    private static string DefaultFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "EMS.Agent", "store-unlock.json");

    private DateTime? ReadUnlockUntil()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<UnlockDocument>(File.ReadAllText(_filePath))?.UnlockedUntil;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the Store unlock file {Path}.", _filePath);
            return null;
        }
    }

    private sealed class UnlockDocument
    {
        public DateTime UnlockedUntil { get; set; }
    }
}
