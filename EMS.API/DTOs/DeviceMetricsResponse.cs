namespace EMS.API.DTOs;

/// <summary>
/// The most recent live-monitoring snapshot for a device, plus the freshness
/// information the dashboard needs to decide whether it is still current.
/// </summary>
public class DeviceMetricsResponse
{
    /// <summary>When this snapshot was taken (the heartbeat time, UTC).</summary>
    public DateTime? CollectedAt { get; set; }

    /// <summary>
    /// True when the last heartbeat is recent enough that the device counts
    /// as online. Computed server-side so every client agrees on the rule.
    /// </summary>
    public bool IsOnline { get; set; }

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
