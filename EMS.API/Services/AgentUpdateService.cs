using System.Security.Cryptography;
using EMS.API.Configuration;
using Microsoft.Extensions.Options;

namespace EMS.API.Services;

public class AgentUpdateService : IAgentUpdateService
{
    private readonly AgentUpdateSettings _settings;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AgentUpdateService> _logger;

    private string? _runtimeVersion;
    private string? _runtimeSha256;
    private string? _resolvedInstallerPath;

    public AgentUpdateService(
        IOptions<AgentUpdateSettings> settings,
        IWebHostEnvironment environment,
        ILogger<AgentUpdateService> logger)
    {
        _settings = settings.Value;
        _environment = environment;
        _logger = logger;

        _runtimeVersion = _settings.LatestVersion;
        _runtimeSha256 = _settings.Sha256;

        InitializeInstallerPath();
    }

    private void InitializeInstallerPath()
    {
        var rawPath = _settings.InstallerPath;
        if (Path.IsPathRooted(rawPath))
        {
            _resolvedInstallerPath = rawPath;
        }
        else
        {
            _resolvedInstallerPath = Path.Combine(_environment.ContentRootPath, rawPath);
        }

        // Auto-compute SHA-256 if file exists and hash is not configured
        if (File.Exists(_resolvedInstallerPath) && string.IsNullOrWhiteSpace(_runtimeSha256))
        {
            try
            {
                using var stream = File.OpenRead(_resolvedInstallerPath);
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(stream);
                _runtimeSha256 = Convert.ToHexString(hashBytes).ToLowerInvariant();
                _logger.LogInformation("Computed SHA-256 for agent installer: {Sha256}", _runtimeSha256);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not compute SHA-256 for installer at {Path}.", _resolvedInstallerPath);
            }
        }
    }

    public AgentUpdateInfo GetUpdateInfo()
    {
        var hasFile = !string.IsNullOrEmpty(_resolvedInstallerPath) && File.Exists(_resolvedInstallerPath);
        return new AgentUpdateInfo(
            Enabled: _settings.Enabled,
            LatestVersion: _runtimeVersion,
            DownloadUrl: _settings.DownloadUrl,
            Sha256: _runtimeSha256,
            HasInstallerFile: hasFile);
    }

    public Task<Stream?> GetInstallerStreamAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_resolvedInstallerPath) || !File.Exists(_resolvedInstallerPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        try
        {
            Stream stream = new FileStream(
                _resolvedInstallerPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                useAsync: true);

            return Task.FromResult<Stream?>(stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open installer file stream at {Path}.", _resolvedInstallerPath);
            return Task.FromResult<Stream?>(null);
        }
    }

    public string GetInstallerFileName()
    {
        if (!string.IsNullOrEmpty(_resolvedInstallerPath))
        {
            return Path.GetFileName(_resolvedInstallerPath);
        }
        return "TheLanceEMSSetup.exe";
    }

    public async Task<AgentUpdateInfo> RegisterUpdatePackageAsync(
        string version, Stream fileStream, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Version cannot be empty.", nameof(version));
        }

        var dir = Path.GetDirectoryName(_resolvedInstallerPath)
            ?? Path.Combine(_environment.ContentRootPath, "updates");

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var targetPath = Path.Combine(dir, $"TheLanceEMSSetup-{version}.exe");
        var tempPath = Path.Combine(dir, $"upload-{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var outputStream = File.Create(tempPath))
            {
                await fileStream.CopyToAsync(outputStream, cancellationToken);
            }

            // Compute hash
            string computedHash;
            await using (var verifyStream = File.OpenRead(tempPath))
            {
                var hashBytes = await SHA256.HashDataAsync(verifyStream, cancellationToken);
                computedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }

            // Atomically replace/move to targetPath
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            File.Move(tempPath, targetPath);

            // Also update default path copy if targetPath is different
            if (!string.Equals(_resolvedInstallerPath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(targetPath, _resolvedInstallerPath!, overwrite: true);
            }

            _runtimeVersion = version;
            _runtimeSha256 = computedHash;

            _logger.LogInformation("New agent update registered. Version: {Version}, SHA-256: {Sha256}",
                version, computedHash);

            return GetUpdateInfo();
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best effort */ }
            }
        }
    }
}
