namespace EMS.API.DTOs;

public class HeartbeatResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>Server UTC time; lets agents detect clock drift.</summary>
    public DateTime ServerTime { get; set; }
}
