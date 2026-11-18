using System.Reflection;
using System.Runtime.Versioning;
using EMS.Agent.Helpers;

namespace EMS.Agent.Services;

[SupportedOSPlatform("windows")]
public class AutoUpdateService : IAutoUpdateService
{
    private static readonly string CurrentVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    private static readonly TimeSpan RetryCooldown = TimeSpan.FromMinutes(10);

    private readonly IApiClientService _apiClient;
    private readonly ILogger<AutoUpdateService> _logger;

    private int _isUpdating = 0;
    private DateTime _lastAttemptUtc = DateTime.MinValue;

    public AutoUpdateService(
        IApiClientService apiClient,
        ILogger<AutoUpdateService> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public void CheckAndTriggerUpdate(string? latestVersion, string? downloadUrl, string? expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(latestVersion) || string.IsNullOrWhiteSpace(downloadUrl))
        {
            return;
        }

        if (!IsNewerVersion(latestVersion, CurrentVersion))
        {
            return;
        }

        // Only run one update attempt at a time
        if (Interlocked.CompareExchange(ref _isUpdating, 1, 0) != 0)
        {
            _logger.LogDebug("Auto-update already in progress; skipping trigger.");
            return;
        }

        // Prevent hammering the server if an update attempt failed recently
        if (DateTime.UtcNow - _lastAttemptUtc < RetryCooldown)
        {
            _logger.LogDebug("Auto-update in cooldown period until {Time}.", _lastAttemptUtc + RetryCooldown);
            Interlocked.Exchange(ref _isUpdating, 0);
            return;
        }

        _logger.LogInformation(
            "New agent version {LatestVersion} available (current: {CurrentVersion}). Initiating auto-update...",
            latestVersion, CurrentVersion);

        _lastAttemptUtc = DateTime.UtcNow;

        _ = Task.Run(async () =>
        {
            try
            {
                await RunUpdateAsync(latestVersion, downloadUrl, expectedSha256);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred during auto-update to version {LatestVersion}.", latestVersion);
            }
            finally
            {
                Interlocked.Exchange(ref _isUpdating, 0);
            }
        });
    }

    private async Task RunUpdateAsync(string latestVersion, string downloadUrl, string? expectedSha256)
    {
        var updatesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "EMS.Agent", "updates");

        if (!Directory.Exists(updatesDir))
        {
            Directory.CreateDirectory(updatesDir);
        }

        var installerPath = Path.Combine(updatesDir, $"TheLanceEMSSetup-{latestVersion}.exe");

        _logger.LogInformation("Downloading auto-update installer from {Url} to {Path}...", downloadUrl, installerPath);

        var downloaded = await _apiClient.DownloadFileAsync(
            downloadUrl, installerPath, expectedSha256, CancellationToken.None);

        if (!downloaded)
        {
            _logger.LogWarning("Auto-update download failed for version {LatestVersion}.", latestVersion);
            return;
        }

        _logger.LogInformation("Downloaded auto-update installer successfully. Launching detached installer...");
        AutoUpdateLauncher.Launch(installerPath, _logger);
    }

    public static bool IsNewerVersion(string? latestStr, string currentStr)
    {
        if (string.IsNullOrWhiteSpace(latestStr))
        {
            return false;
        }

        var cleanedLatest = latestStr.Trim().TrimStart('v', 'V');
        var cleanedCurrent = currentStr.Trim().TrimStart('v', 'V');

        if (Version.TryParse(cleanedLatest, out var latest) && Version.TryParse(cleanedCurrent, out var current))
        {
            return latest > current;
        }

        return !string.Equals(cleanedLatest, cleanedCurrent, StringComparison.OrdinalIgnoreCase);
    }
}
