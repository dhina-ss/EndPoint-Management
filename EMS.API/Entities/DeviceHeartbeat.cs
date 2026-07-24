namespace EMS.API.Entities;

/// <summary>
/// One heartbeat ping from an agent. The history allows uptime/online
/// reporting; the device's current state lives in Device.LastHeartbeatTime.
///
/// Each row also carries the live resource snapshot taken at that moment, so
/// this table doubles as the live-monitoring time series. All metric columns
/// are nullable: older agents do not send them, and an individual metric can
/// fail to read without invalidating the heartbeat.
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

    public double? CpuUsagePercent { get; set; }

    public double? MemoryUsagePercent { get; set; }

    public int? MemoryUsedMb { get; set; }

    public int? MemoryTotalMb { get; set; }

    public double? DiskUsagePercent { get; set; }

    public int? DiskUsedGb { get; set; }

    public int? DiskTotalGb { get; set; }

    public double? NetworkSentKbps { get; set; }

    public double? NetworkReceivedKbps { get; set; }

    public long? UptimeSeconds { get; set; }

    public int? BatteryPercent { get; set; }

    public bool? BatteryCharging { get; set; }

    public bool? HasBattery { get; set; }
}
