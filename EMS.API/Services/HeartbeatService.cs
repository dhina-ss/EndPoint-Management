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

        await _heartbeatRepository.AddAsync(new DeviceHeartbeat
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            IPAddress = request.IPAddress,
            Username = request.Username,
            AgentVersion = request.AgentVersion,
            HeartbeatTime = utcNow
        }, cancellationToken);

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
}
