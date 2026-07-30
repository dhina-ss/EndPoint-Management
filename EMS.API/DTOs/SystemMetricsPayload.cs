namespace EMS.API.DTOs;

/// <summary>
/// Live resource snapshot sent with a heartbeat. Every field is optional:
/// agents that predate live monitoring omit the whole object, and a single
/// unreadable metric arrives as null rather than failing the heartbeat.
/// </summary>
public class SystemMetricsPayload
{
    public double? CpuUsagePercent { get; set; }

    public double? MemoryUsagePercent { get; set; }

    public int? MemoryUsedMb { get; set; }

    public int? MemoryTotalMb { get; set; }

    public double? DiskUsagePercent { get; set; }

    public int? DiskUsedGb { get; set; }

    public int? DiskTotalGb { get; set; }

    public double? NetworkSentKbps { get; set; }

    public double? NetworkReceivedKbps { get; set; }

    /// <summary>Bytes sent since the previous sample; accumulated into daily usage.</summary>
    public long? NetworkBytesSentDelta { get; set; }

    /// <summary>Bytes received since the previous sample; accumulated into daily usage.</summary>
    public long? NetworkBytesReceivedDelta { get; set; }

    public long? UptimeSeconds { get; set; }

    public int? BatteryPercent { get; set; }

    public bool? BatteryCharging { get; set; }

    public bool? HasBattery { get; set; }
}
