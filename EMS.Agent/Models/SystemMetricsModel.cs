namespace EMS.Agent.Models;

/// <summary>
/// Live resource snapshot, collected with each heartbeat. Every field is
/// nullable: a metric that cannot be read on a given machine (no battery, a
/// failed WMI query) is simply omitted rather than failing the heartbeat.
/// </summary>
public class SystemMetricsModel
{
    public double? CpuUsagePercent { get; set; }

    public double? MemoryUsagePercent { get; set; }

    public int? MemoryUsedMb { get; set; }

    public int? MemoryTotalMb { get; set; }

    public double? DiskUsagePercent { get; set; }

    public int? DiskUsedGb { get; set; }

    public int? DiskTotalGb { get; set; }

    /// <summary>Send rate since the previous sample, in KB/s.</summary>
    public double? NetworkSentKbps { get; set; }

    /// <summary>Receive rate since the previous sample, in KB/s.</summary>
    public double? NetworkReceivedKbps { get; set; }

    /// <summary>Seconds since the machine last booted.</summary>
    public long? UptimeSeconds { get; set; }

    public int? BatteryPercent { get; set; }

    public bool? BatteryCharging { get; set; }

    /// <summary>False on desktops/VMs with no battery present.</summary>
    public bool? HasBattery { get; set; }
}
