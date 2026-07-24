using System.Diagnostics;
using System.Runtime.Versioning;
using EMS.Agent.Helpers;
using EMS.Agent.Models;

namespace EMS.Agent.Services;

/// <summary>
/// Assembles the live metrics snapshot. Registered as a singleton because
/// network throughput is a rate: it needs the previous sample's byte counters
/// and timestamp, which must survive between heartbeats.
///
/// Each metric is collected independently so one failing source (e.g. WMI
/// refusing a query) degrades that field to null instead of losing the
/// whole snapshot.
/// </summary>
[SupportedOSPlatform("windows")]
public class SystemMetricsService : ISystemMetricsService
{
    private readonly ILogger<SystemMetricsService> _logger;
    private readonly object _lock = new();

    private long _lastBytesSent;
    private long _lastBytesReceived;
    private long _lastSampleTimestamp;

    public SystemMetricsService(ILogger<SystemMetricsService> logger)
    {
        _logger = logger;
    }

    public SystemMetricsModel Collect()
    {
        var metrics = new SystemMetricsModel();

        Try("CPU", () => metrics.CpuUsagePercent = Round(SystemMetricsHelper.GetCpuUsagePercent()));

        Try("memory", () =>
        {
            var memory = SystemMetricsHelper.GetMemoryUsage();
            if (memory is null)
            {
                return;
            }

            metrics.MemoryUsedMb = memory.Value.UsedMb;
            metrics.MemoryTotalMb = memory.Value.TotalMb;
            metrics.MemoryUsagePercent = Percent(memory.Value.UsedMb, memory.Value.TotalMb);
        });

        Try("disk", () =>
        {
            var disk = SystemMetricsHelper.GetSystemDiskUsage();
            if (disk is null)
            {
                return;
            }

            metrics.DiskUsedGb = disk.Value.UsedGb;
            metrics.DiskTotalGb = disk.Value.TotalGb;
            metrics.DiskUsagePercent = Percent(disk.Value.UsedGb, disk.Value.TotalGb);
        });

        Try("network", () => CollectNetworkRates(metrics));

        Try("uptime", () => metrics.UptimeSeconds = SystemMetricsHelper.GetUptimeSeconds());

        Try("battery", () =>
        {
            var (hasBattery, percent, charging) = SystemMetricsHelper.GetBatteryStatus();
            metrics.HasBattery = hasBattery;
            metrics.BatteryPercent = percent;
            metrics.BatteryCharging = charging;
        });

        return metrics;
    }

    private void CollectNetworkRates(SystemMetricsModel metrics)
    {
        var (bytesSent, bytesReceived) = SystemMetricsHelper.GetNetworkTotals();
        var now = Stopwatch.GetTimestamp();

        lock (_lock)
        {
            if (_lastSampleTimestamp != 0)
            {
                var elapsedSeconds = (now - _lastSampleTimestamp) / (double)Stopwatch.Frequency;

                // Counters reset when an adapter is disabled or the machine
                // sleeps; a negative delta means the baseline is stale, so
                // skip this reading rather than reporting a bogus spike.
                if (elapsedSeconds > 0 && bytesSent >= _lastBytesSent && bytesReceived >= _lastBytesReceived)
                {
                    metrics.NetworkSentKbps = Round((bytesSent - _lastBytesSent) / 1024d / elapsedSeconds);
                    metrics.NetworkReceivedKbps = Round((bytesReceived - _lastBytesReceived) / 1024d / elapsedSeconds);
                }
            }

            _lastBytesSent = bytesSent;
            _lastBytesReceived = bytesReceived;
            _lastSampleTimestamp = now;
        }
    }

    private void Try(string metricName, Action collect)
    {
        try
        {
            collect();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not collect {Metric} metrics for this heartbeat.", metricName);
        }
    }

    private static double? Round(double? value) => value is null ? null : Math.Round(value.Value, 1);

    private static double? Percent(int used, int total)
        => total <= 0 ? null : Math.Round(used * 100d / total, 1);
}
