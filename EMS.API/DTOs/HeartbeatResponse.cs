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
    /// Device-specific domains to block, on top of the agent's always-on
    /// default phishing/malware list. The agent merges the two and writes
    /// them to the hosts file after each heartbeat.
    /// </summary>
    public IReadOnlyList<string> BlockedWebsites { get; set; } = [];

    /// <summary>
    /// Executable names the agent must prevent from launching on this
    /// device, e.g. "chrome.exe". Applied after each heartbeat.
    /// </summary>
    public IReadOnlyList<string> BlockedApplications { get; set; } = [];
}
