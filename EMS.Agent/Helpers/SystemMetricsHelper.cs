using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.Versioning;

namespace EMS.Agent.Helpers;

/// <summary>
/// Point-in-time readings of CPU, memory, disk, network counters, uptime and
/// battery. Each method is independent and lets its exception surface, so the
/// caller can degrade one metric without losing the rest.
/// </summary>
[SupportedOSPlatform("windows")]
public static class SystemMetricsHelper
{
    /// <summary>
    /// Current total CPU load, 0-100. Prefers the live performance counter
    /// class; falls back to Win32_Processor, which is coarser but always
    /// present.
    /// </summary>
    public static double? GetCpuUsagePercent()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'");
            foreach (var obj in searcher.Get())
            {
                if (obj["PercentProcessorTime"] is not null)
                {
                    return Convert.ToDouble(obj["PercentProcessorTime"]);
                }
            }
        }
        catch
        {
            // Fall through to the simpler class below.
        }

        using var fallback = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
        var readings = new List<double>();
        foreach (var obj in fallback.Get())
        {
            if (obj["LoadPercentage"] is not null)
            {
                readings.Add(Convert.ToDouble(obj["LoadPercentage"]));
            }
        }

        return readings.Count > 0 ? readings.Average() : null;
    }

    /// <summary>Physical memory used and total, in MB.</summary>
    public static (int UsedMb, int TotalMb)? GetMemoryUsage()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");

        foreach (var obj in searcher.Get())
        {
            // Both values are reported in kilobytes.
            var totalKb = Convert.ToInt64(obj["TotalVisibleMemorySize"] ?? 0L);
            var freeKb = Convert.ToInt64(obj["FreePhysicalMemory"] ?? 0L);

            if (totalKb <= 0)
            {
                return null;
            }

            var usedKb = Math.Max(0, totalKb - freeKb);
            return ((int)(usedKb / 1024), (int)(totalKb / 1024));
        }

        return null;
    }

    /// <summary>
    /// Used and total space on the system drive, in GB. The system drive is
    /// the actionable one for "disk full" monitoring.
    /// </summary>
    public static (int UsedGb, int TotalGb)? GetSystemDiskUsage()
    {
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
        if (string.IsNullOrEmpty(systemRoot))
        {
            return null;
        }

        var drive = new DriveInfo(systemRoot);
        if (!drive.IsReady || drive.TotalSize <= 0)
        {
            return null;
        }

        const double BytesPerGb = 1024d * 1024d * 1024d;
        var used = drive.TotalSize - drive.TotalFreeSpace;

        return ((int)Math.Round(used / BytesPerGb), (int)Math.Round(drive.TotalSize / BytesPerGb));
    }

    /// <summary>
    /// Cumulative bytes sent/received across all operational, non-loopback
    /// adapters. Rates are derived by comparing two samples over time.
    /// </summary>
    public static (long BytesSent, long BytesReceived) GetNetworkTotals()
    {
        long sent = 0;
        long received = 0;

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up ||
                nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            try
            {
                var stats = nic.GetIPStatistics();
                sent += stats.BytesSent;
                received += stats.BytesReceived;
            }
            catch
            {
                // Some virtual adapters refuse statistics; skip them.
            }
        }

        return (sent, received);
    }

    /// <summary>Seconds since the last boot.</summary>
    public static long GetUptimeSeconds() => Environment.TickCount64 / 1000;

    /// <summary>
    /// Battery charge and charging state, or HasBattery=false on machines
    /// with no battery (desktops, most VMs).
    /// </summary>
    public static (bool HasBattery, int? Percent, bool? Charging) GetBatteryStatus()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery");

        foreach (var obj in searcher.Get())
        {
            int? percent = obj["EstimatedChargeRemaining"] is null
                ? null
                : Convert.ToInt32(obj["EstimatedChargeRemaining"]);

            bool? charging = null;
            if (obj["BatteryStatus"] is not null)
            {
                // 2 = on AC, 6/7/8/9 = charging variants; 1 and 4/5 = discharging.
                var status = Convert.ToInt32(obj["BatteryStatus"]);
                charging = status is 2 or 6 or 7 or 8 or 9;
            }

            return (true, percent, charging);
        }

        return (false, null, null);
    }
}
