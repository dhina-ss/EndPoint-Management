using System.Runtime.Versioning;
using EMS.Agent.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace EMS.Agent.Tests;

/// <summary>
/// Exercises metrics collection against the real machine. These assert
/// plausible ranges rather than exact values - the point is that each source
/// actually reads something on a real Windows host.
/// </summary>
[SupportedOSPlatform("windows")]
public class SystemMetricsServiceTests
{
    private static SystemMetricsService CreateService()
        => new(NullLogger<SystemMetricsService>.Instance);

    [Fact]
    public void Collect_ReturnsPlausibleCpuMemoryAndDisk()
    {
        var metrics = CreateService().Collect();

        Assert.NotNull(metrics.CpuUsagePercent);
        Assert.InRange(metrics.CpuUsagePercent!.Value, 0, 100);

        Assert.NotNull(metrics.MemoryUsagePercent);
        Assert.InRange(metrics.MemoryUsagePercent!.Value, 0, 100);
        Assert.True(metrics.MemoryTotalMb > 0);
        Assert.True(metrics.MemoryUsedMb <= metrics.MemoryTotalMb);

        Assert.NotNull(metrics.DiskUsagePercent);
        Assert.InRange(metrics.DiskUsagePercent!.Value, 0, 100);
        Assert.True(metrics.DiskTotalGb > 0);
        Assert.True(metrics.DiskUsedGb <= metrics.DiskTotalGb);
    }

    [Fact]
    public void Collect_ReturnsUptimeAndBatteryPresence()
    {
        var metrics = CreateService().Collect();

        Assert.NotNull(metrics.UptimeSeconds);
        Assert.True(metrics.UptimeSeconds > 0);

        // HasBattery is true on laptops, false on desktops - both are valid,
        // but the field must be populated either way.
        Assert.NotNull(metrics.HasBattery);
        if (metrics.HasBattery == true && metrics.BatteryPercent is not null)
        {
            Assert.InRange(metrics.BatteryPercent.Value, 0, 100);
        }
    }

    [Fact]
    public async Task Collect_NetworkRates_RequireTwoSamples()
    {
        var service = CreateService();

        // Rates are deltas: the first sample only establishes the baseline.
        var first = service.Collect();
        Assert.Null(first.NetworkSentKbps);
        Assert.Null(first.NetworkReceivedKbps);

        await Task.Delay(1200);
        var second = service.Collect();

        Assert.NotNull(second.NetworkSentKbps);
        Assert.NotNull(second.NetworkReceivedKbps);
        Assert.True(second.NetworkSentKbps >= 0);
        Assert.True(second.NetworkReceivedKbps >= 0);
    }
}
