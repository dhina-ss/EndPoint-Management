using EMS.API.DTOs;
using EMS.API.Entities;
using EMS.API.Repositories;

namespace EMS.API.Services;

public class HeartbeatService : IHeartbeatService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IHeartbeatRepository _heartbeatRepository;
    private readonly IBlockedWebsiteRepository _blockedWebsiteRepository;
    private readonly INetworkUsageRepository _networkUsageRepository;
    private readonly ILogger<HeartbeatService> _logger;

    public HeartbeatService(
        IDeviceRepository deviceRepository,
        IHeartbeatRepository heartbeatRepository,
        IBlockedWebsiteRepository blockedWebsiteRepository,
        INetworkUsageRepository networkUsageRepository,
        ILogger<HeartbeatService> logger)
    {
        _deviceRepository = deviceRepository;
        _heartbeatRepository = heartbeatRepository;
        _blockedWebsiteRepository = blockedWebsiteRepository;
        _networkUsageRepository = networkUsageRepository;
        _logger = logger;
    }

    public async Task<HeartbeatResponse?> RecordHeartbeatAsync(
        string deviceId, HeartbeatRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Heartbeat received from device {DeviceId}.", deviceId);

        var device = await _deviceRepository.GetByDeviceIdAsync(deviceId, cancellationToken);
        if (device is null)
        {
            // Token validation passed, so this only happens if the device row
            // was deleted after its credential check.
            _logger.LogWarning("Heartbeat rejected: device {DeviceId} not found.", deviceId);
            return null;
        }

        _logger.LogInformation("Device {DeviceId} validated for heartbeat.", deviceId);

        var utcNow = DateTime.UtcNow;

        device.LastHeartbeatTime = utcNow;
        device.LastSeen = utcNow;

        var heartbeat = new DeviceHeartbeat
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            IPAddress = request.IPAddress,
            Username = request.Username,
            AgentVersion = request.AgentVersion,
            HeartbeatTime = utcNow
        };

        ApplyMetrics(heartbeat, request.Metrics);
        await _heartbeatRepository.AddAsync(heartbeat, cancellationToken);

        await AccumulateNetworkUsageAsync(device.Id, request.Metrics, utcNow, cancellationToken);

        // One SaveChanges persists the heartbeat row, the updated device
        // timestamps, and the network-usage upsert — they share the scoped
        // DbContext.
        await _heartbeatRepository.SaveChangesAsync(cancellationToken);

        var blockedWebsites = await _blockedWebsiteRepository.GetByDeviceAsync(device.Id, cancellationToken);

        return new HeartbeatResponse
        {
            Success = true,
            Message = "Heartbeat received",
            ServerTime = utcNow,
            UsbBlockingEnabled = device.UsbBlockingEnabled,
            StoreGatingEnabled = device.StoreGatingEnabled,
            BlockedWebsites = blockedWebsites.Select(b => b.Domain).ToList()
        };
    }

    /// <summary>
    /// A device counts as online when its last heartbeat is within three
    /// intervals (3 x 60s), i.e. three missed beats. Kept server-side so the
    /// dashboard, alerts and reports can never disagree on the rule.
    /// </summary>
    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(3);

    public async Task<DeviceMetricsResponse?> GetLatestMetricsAsync(
        Guid deviceInternalId, CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceInternalId, cancellationToken);
        if (device is null)
        {
            return null;
        }

        var isOnline = device.LastHeartbeatTime is not null
            && DateTime.UtcNow - device.LastHeartbeatTime.Value < OnlineThreshold;

        var latest = await _heartbeatRepository.GetLatestForDeviceAsync(deviceInternalId, cancellationToken);
        if (latest is null)
        {
            return new DeviceMetricsResponse { IsOnline = isOnline };
        }

        return new DeviceMetricsResponse
        {
            CollectedAt = latest.HeartbeatTime,
            IsOnline = isOnline,
            CpuUsagePercent = latest.CpuUsagePercent,
            MemoryUsagePercent = latest.MemoryUsagePercent,
            MemoryUsedMb = latest.MemoryUsedMb,
            MemoryTotalMb = latest.MemoryTotalMb,
            DiskUsagePercent = latest.DiskUsagePercent,
            DiskUsedGb = latest.DiskUsedGb,
            DiskTotalGb = latest.DiskTotalGb,
            NetworkSentKbps = latest.NetworkSentKbps,
            NetworkReceivedKbps = latest.NetworkReceivedKbps,
            UptimeSeconds = latest.UptimeSeconds,
            BatteryPercent = latest.BatteryPercent,
            BatteryCharging = latest.BatteryCharging,
            HasBattery = latest.HasBattery
        };
    }

    /// <summary>
    /// Adds this heartbeat's byte deltas to the device's running total for
    /// today (UTC), inserting the day's row on first sight. Added to the shared
    /// DbContext; the caller's SaveChanges persists it.
    /// </summary>
    private async Task AccumulateNetworkUsageAsync(
        Guid deviceId, SystemMetricsPayload? metrics, DateTime utcNow, CancellationToken cancellationToken)
    {
        var sent = metrics?.NetworkBytesSentDelta ?? 0;
        var received = metrics?.NetworkBytesReceivedDelta ?? 0;
        if (sent <= 0 && received <= 0)
        {
            return;
        }

        var today = DateOnly.FromDateTime(utcNow);
        var record = await _networkUsageRepository.GetTrackedAsync(deviceId, today, cancellationToken);

        if (record is null)
        {
            await _networkUsageRepository.AddAsync(new NetworkUsageRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                UsageDate = today,
                BytesSent = sent,
                BytesReceived = received,
                LastUpdated = utcNow
            }, cancellationToken);
        }
        else
        {
            record.BytesSent += sent;
            record.BytesReceived += received;
            record.LastUpdated = utcNow;
        }
    }

    /// <summary>Daily network-usage totals for a device over the last N days.</summary>
    public async Task<IReadOnlyList<NetworkUsageResponse>?> GetNetworkUsageAsync(
        Guid deviceInternalId, int days, CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceInternalId, cancellationToken);
        if (device is null)
        {
            return null;
        }

        var fromDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-(Math.Max(1, days) - 1));
        var records = await _networkUsageRepository.GetByDeviceSinceAsync(deviceInternalId, fromDate, cancellationToken);

        return records.Select(r => new NetworkUsageResponse
        {
            UsageDate = r.UsageDate,
            BytesSent = r.BytesSent,
            BytesReceived = r.BytesReceived
        }).ToList();
    }

    private static void ApplyMetrics(DeviceHeartbeat heartbeat, SystemMetricsPayload? metrics)
    {
        if (metrics is null)
        {
            return;
        }

        heartbeat.CpuUsagePercent = metrics.CpuUsagePercent;
        heartbeat.MemoryUsagePercent = metrics.MemoryUsagePercent;
        heartbeat.MemoryUsedMb = metrics.MemoryUsedMb;
        heartbeat.MemoryTotalMb = metrics.MemoryTotalMb;
        heartbeat.DiskUsagePercent = metrics.DiskUsagePercent;
        heartbeat.DiskUsedGb = metrics.DiskUsedGb;
        heartbeat.DiskTotalGb = metrics.DiskTotalGb;
        heartbeat.NetworkSentKbps = metrics.NetworkSentKbps;
        heartbeat.NetworkReceivedKbps = metrics.NetworkReceivedKbps;
        heartbeat.UptimeSeconds = metrics.UptimeSeconds;
        heartbeat.BatteryPercent = metrics.BatteryPercent;
        heartbeat.BatteryCharging = metrics.BatteryCharging;
        heartbeat.HasBattery = metrics.HasBattery;
    }
}
