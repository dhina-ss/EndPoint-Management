namespace EMS.API.DTOs;

public class HeartbeatResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>Server UTC time; lets agents detect clock drift.</summary>
    public DateTime ServerTime { get; set; }

    /// <summary>
    /// Current desired USB mass-storage blocking state for this device; the
    /// agent applies it after each heartbeat, so a dashboard toggle takes
    /// effect within one heartbeat interval.
    /// </summary>
    public bool UsbBlockingEnabled { get; set; }

    /// <summary>
    /// When true, the agent keeps the Microsoft Store disabled unless a valid
    /// local unlock is active. Applied after each heartbeat.
    /// </summary>
    public bool StoreGatingEnabled { get; set; }

    /// <summary>
    /// When true, the agent requires the user to sign in on each device
    /// startup; the agent applies it after this heartbeat.
    /// </summary>
    public bool RequireLoginEachStartup { get; set; }

    /// <summary>
    /// Device-specific domains to block, on top of the agent's always-on
    /// default phishing/malware list. The agent merges the two and writes
    /// them to the hosts file after each heartbeat.
    /// </summary>
    public IReadOnlyList<string> BlockedWebsites { get; set; } = [];

    /// <summary>
    /// If an agent update is available, the latest agent version string (e.g. "1.0.28").
    /// </summary>
    public string? LatestAgentVersion { get; set; }

    /// <summary>
    /// Relative or absolute URL where the update installer binary can be downloaded.
    /// </summary>
    public string? UpdateDownloadUrl { get; set; }

    /// <summary>
    /// Expected SHA-256 hash of the installer for verification before running.
    /// </summary>
    public string? UpdateSha256 { get; set; }
}
