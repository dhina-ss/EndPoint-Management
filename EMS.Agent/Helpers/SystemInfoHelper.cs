using System.Management;
using System.Runtime.Versioning;

namespace EMS.Agent.Helpers;

/// <summary>
/// Low-level WMI queries. Each method queries exactly one WMI class and
/// lets exceptions bubble up — <see cref="Services.DeviceCollectorService"/>
/// decides how failures affect the overall inventory.
/// </summary>
[SupportedOSPlatform("windows")]
public static class SystemInfoHelper
{
    private const double BytesPerGigabyte = 1024d * 1024d * 1024d;

    public static (string? Manufacturer, string? Model, ulong TotalMemoryBytes, string? LoggedOnUser) GetComputerSystemInfo()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT Manufacturer, Model, TotalPhysicalMemory, UserName FROM Win32_ComputerSystem");

        foreach (var obj in searcher.Get())
        {
            return (
                obj["Manufacturer"]?.ToString(),
                obj["Model"]?.ToString(),
                (ulong?)obj["TotalPhysicalMemory"] ?? 0,
                obj["UserName"]?.ToString());
        }

        return (null, null, 0, null);
    }

    public static string? GetBiosSerialNumber()
    {
        using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS");
        foreach (var obj in searcher.Get())
        {
            return obj["SerialNumber"]?.ToString()?.Trim();
        }

        return null;
    }

    public static string? GetProcessorName()
    {
        using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
        foreach (var obj in searcher.Get())
        {
            return obj["Name"]?.ToString()?.Trim();
        }

        return null;
    }

    public static (string? Caption, string? BuildNumber, DateTime? LastBootTime) GetOperatingSystemInfo()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT Caption, BuildNumber, LastBootUpTime FROM Win32_OperatingSystem");

        foreach (var obj in searcher.Get())
        {
            var lastBootRaw = obj["LastBootUpTime"]?.ToString();
            DateTime? lastBoot = string.IsNullOrWhiteSpace(lastBootRaw)
                ? null
                : ManagementDateTimeConverter.ToDateTime(lastBootRaw);

            return (
                obj["Caption"]?.ToString()?.Trim(),
                obj["BuildNumber"]?.ToString(),
                lastBoot);
        }

        return (null, null, null);
    }

    /// <summary>Total capacity of all fixed (local) disks.</summary>
    public static ulong GetTotalFixedDiskBytes()
    {
        ulong total = 0;

        using var searcher = new ManagementObjectSearcher(
            "SELECT Size FROM Win32_LogicalDisk WHERE DriveType = 3");

        foreach (var obj in searcher.Get())
        {
            total += (ulong?)obj["Size"] ?? 0;
        }

        return total;
    }

    /// <summary>
    /// IPv4 and MAC address of the primary adapter — the first IP-enabled
    /// adapter that has a default gateway, falling back to any IP-enabled one.
    /// </summary>
    public static (string? IpAddress, string? MacAddress) GetPrimaryNetworkInfo()
    {
        string? fallbackIp = null;
        string? fallbackMac = null;

        using var searcher = new ManagementObjectSearcher(
            "SELECT IPAddress, MACAddress, DefaultIPGateway FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE");

        foreach (var obj in searcher.Get())
        {
            var mac = obj["MACAddress"]?.ToString();
            var ipv4 = (obj["IPAddress"] as string[])?.FirstOrDefault(ip => ip.Contains('.'));

            if (string.IsNullOrWhiteSpace(ipv4) || string.IsNullOrWhiteSpace(mac))
            {
                continue;
            }

            if (obj["DefaultIPGateway"] is string[] { Length: > 0 })
            {
                return (ipv4, mac);
            }

            fallbackIp ??= ipv4;
            fallbackMac ??= mac;
        }

        return (fallbackIp, fallbackMac);
    }

    public static string FormatBytesAsGigabytes(ulong bytes)
    {
        return $"{Math.Round(bytes / BytesPerGigabyte)} GB";
    }
}
