using EMS.API.DTOs;
using EMS.API.Entities;
using EMS.API.Repositories;

namespace EMS.API.Services;

public class DeviceService : IDeviceService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceAuthService _deviceAuthService;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(
        IDeviceRepository deviceRepository,
        IDeviceAuthService deviceAuthService,
        ILogger<DeviceService> logger)
    {
        _deviceRepository = deviceRepository;
        _deviceAuthService = deviceAuthService;
        _logger = logger;
    }

    public async Task<DeviceRegisterResponse> RegisterAsync(
        DeviceRegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Device registration started for DeviceId {DeviceId}", request.DeviceId);

        var utcNow = DateTime.UtcNow;
        var device = await _deviceRepository.GetByDeviceIdAsync(request.DeviceId, cancellationToken);

        if (device is null)
        {
            device = new Device
            {
                Id = Guid.NewGuid(),
                DeviceId = request.DeviceId,
                CreatedDate = utcNow
            };

            ApplyInventory(device, request, utcNow);
            await _deviceRepository.AddAsync(device, cancellationToken);
        }
        else
        {
            // Registration is idempotent: a known device re-registering refreshes
            // its inventory and LastSeen instead of failing on the unique DeviceId.
            ApplyInventory(device, request, utcNow);
        }

        await _deviceRepository.SaveChangesAsync(cancellationToken);

        // Every successful registration (re)issues the device token; the agent
        // stores the latest one and uses it for authenticated calls.
        var token = await _deviceAuthService.IssueTokenAsync(device, cancellationToken);

        _logger.LogInformation("Device registered successfully for DeviceId {DeviceId}", request.DeviceId);

        return new DeviceRegisterResponse
        {
            Success = true,
            Message = "Device registered successfully",
            DeviceId = request.DeviceId,
            Token = token
        };
    }

    public async Task<IReadOnlyList<DeviceResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var devices = await _deviceRepository.GetAllAsync(cancellationToken);
        return devices.Select(MapToResponse).ToList();
    }

    public async Task<DeviceResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetByIdAsync(id, cancellationToken);
        return device is null ? null : MapToResponse(device);
    }

    private static void ApplyInventory(Device device, DeviceRegisterRequest request, DateTime utcNow)
    {
        device.DeviceName = request.DeviceName;
        device.SerialNumber = request.SerialNumber;
        device.Manufacturer = request.Manufacturer;
        device.Model = request.Model;
        device.Processor = request.Processor;
        device.RamSize = request.RamSize;
        device.StorageSize = request.StorageSize;
        device.OSVersion = request.OSVersion;
        device.OSBuildNumber = request.OSBuildNumber;
        device.IPAddress = request.IPAddress;
        device.MACAddress = request.MACAddress;
        device.Username = request.Username;
        device.LastBootTime = request.LastBootTime;
        device.UpdatedDate = utcNow;
        device.LastSeen = utcNow;
    }

    private static DeviceResponse MapToResponse(Device device)
    {
        return new DeviceResponse
        {
            Id = device.Id,
            DeviceId = device.DeviceId,
            DeviceName = device.DeviceName,
            SerialNumber = device.SerialNumber,
            Manufacturer = device.Manufacturer,
            Model = device.Model,
            Processor = device.Processor,
            RamSize = device.RamSize,
            StorageSize = device.StorageSize,
            OSVersion = device.OSVersion,
            OSBuildNumber = device.OSBuildNumber,
            IPAddress = device.IPAddress,
            MACAddress = device.MACAddress,
            Username = device.Username,
            LastBootTime = device.LastBootTime,
            CreatedDate = device.CreatedDate,
            UpdatedDate = device.UpdatedDate,
            LastSeen = device.LastSeen,
            LastHeartbeatTime = device.LastHeartbeatTime
        };
    }
}
