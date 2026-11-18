namespace EMS.API.Services;

public record AgentUpdateInfo(
    bool Enabled,
    string? LatestVersion,
    string DownloadUrl,
    string? Sha256,
    bool HasInstallerFile);

public interface IAgentUpdateService
{
    /// <summary>
    /// Returns current agent update availability and metadata.
    /// </summary>
    AgentUpdateInfo GetUpdateInfo();

    /// <summary>
    /// Opens a read stream to the installer binary if present on disk.
    /// </summary>
    Task<Stream?> GetInstallerStreamAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the installer file name for Content-Disposition.
    /// </summary>
    string GetInstallerFileName();

    /// <summary>
    /// Saves a newly uploaded agent installer, computes its SHA-256 hash, and activates it as the latest version.
    /// </summary>
    Task<AgentUpdateInfo> RegisterUpdatePackageAsync(string version, Stream fileStream, CancellationToken cancellationToken = default);
}
