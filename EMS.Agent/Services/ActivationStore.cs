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

    public string? SessionBootId() => ReadDocument()?.SessionBootId;

    public string? LicenseKey() => ReadDocument()?.LicenseKey;

    public void Activate(string activatedBy, string? licenseKey = null)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            var document = new ActivationDocument
            {
                ActivatedBy = activatedBy,
                LicenseKey = licenseKey,
                ActivatedAt = DateTime.UtcNow,
                // Stamp the current power-on session so the require-login-each-
                // startup policy can tell this boot from the next one.
                SessionBootId = SystemInfoHelper.GetBootId()
            };

            UserWritableFile.WriteAllText(_filePath, JsonSerializer.Serialize(document, SerializerOptions));
            _logger.LogInformation("Device activated by {User} (tenant license {License}).",
                activatedBy, string.IsNullOrEmpty(licenseKey) ? "none" : licenseKey);
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

        public string? LicenseKey { get; set; }

        public DateTime ActivatedAt { get; set; }

        public string? SessionBootId { get; set; }
    }
}
