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

    public void Set(bool requireLoginEachStartup)
    {
        // Avoid rewriting (and churning the file ACL) when nothing changed.
        if (ReadDocument()?.RequireLoginEachStartup == requireLoginEachStartup)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            var document = new LoginPolicyDocument
            {
                RequireLoginEachStartup = requireLoginEachStartup,
                UpdatedAt = DateTime.UtcNow
            };

            UserWritableFile.WriteAllText(_filePath, JsonSerializer.Serialize(document, SerializerOptions));
            _logger.LogInformation("Login policy updated: requireLoginEachStartup={Value}.", requireLoginEachStartup);
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

            return JsonSerializer.Deserialize<LoginPolicyDocument>(File.ReadAllText(_filePath));
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

        public DateTime UpdatedAt { get; set; }
    }
}
