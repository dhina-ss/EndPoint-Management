using System.Text.Json;
using EMS.Agent.Helpers;

namespace EMS.Agent.Services;

/// <inheritdoc cref="ILoginPolicyStore"/>
public class LoginPolicyStore : ILoginPolicyStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly ILogger<LoginPolicyStore> _logger;

    public LoginPolicyStore(ILogger<LoginPolicyStore> logger)
        : this(logger, DefaultFilePath())
    {
    }

    /// <summary>Overload for tests; production uses the default path.</summary>
    public LoginPolicyStore(ILogger<LoginPolicyStore> logger, string filePath)
    {
        _logger = logger;
        _filePath = filePath;
    }

    public bool RequireLoginEachStartup() => ReadDocument()?.RequireLoginEachStartup ?? false;

    public int InactivityLockSeconds() => ReadDocument()?.InactivityLockSeconds ?? 0;

    public int InactivityAlertSeconds() => ReadDocument()?.InactivityAlertSeconds ?? 0;

    public void Set(bool requireLoginEachStartup, int inactivityLockSeconds, int inactivityAlertSeconds)
    {
        // Avoid rewriting (and churning the file ACL) when nothing changed.
        var current = ReadDocument();
        if (current?.RequireLoginEachStartup == requireLoginEachStartup
            && current?.InactivityLockSeconds == inactivityLockSeconds
            && current?.InactivityAlertSeconds == inactivityAlertSeconds)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            var document = new LoginPolicyDocument
            {
                RequireLoginEachStartup = requireLoginEachStartup,
                InactivityLockSeconds = inactivityLockSeconds,
                InactivityAlertSeconds = inactivityAlertSeconds,
                UpdatedAt = DateTime.UtcNow
            };

            UserWritableFile.WriteAllText(_filePath, JsonSerializer.Serialize(document, SerializerOptions));
            _logger.LogInformation(
                "Login policy updated: requireLoginEachStartup={Value}, inactivityLock={Lock}s, inactivityAlert={Alert}s.",
                requireLoginEachStartup, inactivityLockSeconds, inactivityAlertSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist login policy to {Path}.", _filePath);
        }
    }

    private static string DefaultFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "EMS.Agent", "login-policy.json");

    private LoginPolicyDocument? ReadDocument()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return JsonSerializer.Deserialize<LoginPolicyDocument>(fs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read login policy file {Path}.", _filePath);
            return null;
        }
    }

    private sealed class LoginPolicyDocument
    {
        public bool RequireLoginEachStartup { get; set; }

        public int InactivityLockSeconds { get; set; }

        public int InactivityAlertSeconds { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
