using EMS.API.DTOs;
using EMS.API.Entities;
using EMS.API.Repositories;

namespace EMS.API.Services;

public class HeartbeatService : IHeartbeatService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IHeartbeatRepository _heartbeatRepository;
    private readonly IBlockedWebsiteRepository _blockedWebsiteRepository;
    private readonly ILogger<HeartbeatService> _logger;

    public HeartbeatService(
        IDeviceRepository deviceRepository,
        IHeartbeatRepository heartbeatRepository,
        IBlockedWebsiteRepository blockedWebsiteRepository,
        ILogger<HeartbeatService> logger)
    {
        _deviceRepository = deviceRepository;
        _heartbeatRepository = heartbeatRepository;
        _blockedWebsiteRepository = blockedWebsiteRepository;
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

        // One SaveChanges persists both the heartbeat row and the updated
        // device timestamps — they share the scoped DbContext.
        await _heartbeatRepository.SaveChangesAsync(cancellationToken);

        var blockedWebsites = await _blockedWebsiteRepository.GetByDeviceAsync(device.Id, cancellationToken);

        return new HeartbeatResponse
        {
            Success = true,
            Message = "Heartbeat received",
            ServerTime = utcNow,
            UsbBlockingEnabled = device.UsbBlockingEnabled,
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
