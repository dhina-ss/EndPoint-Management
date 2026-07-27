using System.Text.Json;
using EMS.Agent.Helpers;

namespace EMS.Agent.Services;

/// <summary>
/// File-backed activation gate: ProgramData\EMS.Agent\activation.json. The
/// login window (user session) writes it; the service (SYSTEM) reads it.
/// Both run under accounts that can access ProgramData.
/// </summary>
public class ActivationStore : IActivationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly ILogger<ActivationStore> _logger;

    public ActivationStore(ILogger<ActivationStore> logger)
        : this(logger, DefaultFilePath())
    {
    }

    /// <summary>Overload for tests; production uses the default path.</summary>
    public ActivationStore(ILogger<ActivationStore> logger, string filePath)
    {
        _logger = logger;
        _filePath = filePath;
    }

    public bool IsActivated() => ReadDocument()?.ActivatedBy is { Length: > 0 };

    public string? ActivatedBy() => ReadDocument()?.ActivatedBy;

    public void Activate(string activatedBy)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            var document = new ActivationDocument
            {
                ActivatedBy = activatedBy,
                ActivatedAt = DateTime.UtcNow
            };

            UserWritableFile.WriteAllText(_filePath, JsonSerializer.Serialize(document, SerializerOptions));
            _logger.LogInformation("Device activated by {User}.", activatedBy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist activation to {Path}.", _filePath);
            throw;
        }
    }

    private static string DefaultFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "EMS.Agent", "activation.json");

    private ActivationDocument? ReadDocument()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<ActivationDocument>(File.ReadAllText(_filePath));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read activation file {Path}.", _filePath);
            return null;
        }
    }

    private sealed class ActivationDocument
    {
        public string? ActivatedBy { get; set; }

        public DateTime ActivatedAt { get; set; }
    }
}
