namespace EMS.API.Configuration;

/// <summary>
/// Configuration for agent auto-updates.
/// </summary>
public class AgentUpdateSettings
{
    public const string SectionName = "AgentUpdate";

    /// <summary>
    /// Whether auto-update distribution is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Latest version available for auto-update, e.g. "1.0.28". If null or empty, auto-update is inactive.
    /// </summary>
    public string? LatestVersion { get; set; }

    /// <summary>
    /// Relative or absolute URL where the agent installer can be downloaded. Defaults to "/api/agent/update/download".
    /// </summary>
    public string DownloadUrl { get; set; } = "/api/agent/update/download";

    /// <summary>
    /// Expected SHA-256 hash of the installer for verification before running.
    /// </summary>
    public string? Sha256 { get; set; }

    /// <summary>
    /// Server-side path to the installer executable, relative to ContentRootPath or absolute.
    /// </summary>
    public string InstallerPath { get; set; } = "updates/TheLanceEMSSetup.exe";
}
