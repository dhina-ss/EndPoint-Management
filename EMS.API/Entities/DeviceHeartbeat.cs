namespace EMS.API.Entities;

/// <summary>
/// One heartbeat ping from an agent. The history allows uptime/online
/// reporting; the device's current state lives in Device.LastHeartbeatTime.
/// </summary>
public class DeviceHeartbeat
{
    public Guid Id { get; set; }

    /// <summary>Foreign key to <see cref="Device.Id"/>.</summary>
    public Guid DeviceId { get; set; }

    public Device Device { get; set; } = null!;

    public string? IPAddress { get; set; }

    public string? Username { get; set; }

    public string? AgentVersion { get; set; }

    public DateTime HeartbeatTime { get; set; }
}
