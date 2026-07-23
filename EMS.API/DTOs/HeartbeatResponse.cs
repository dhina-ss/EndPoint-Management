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
}
