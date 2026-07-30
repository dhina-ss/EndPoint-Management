using EMS.API.DTOs;
using EMS.API.Entities;
using EMS.API.Repositories;

namespace EMS.API.Services;

public class DeviceService : IDeviceService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceAuthService _deviceAuthService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(
        IDeviceRepository deviceRepository,
        IDeviceAuthService deviceAuthService,
        IUserRepository userRepository,
        ILogger<DeviceService> logger)
    {
        _deviceRepository = deviceRepository;
        _deviceAuthService = deviceAuthService;
        _userRepository = userRepository;
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

        await MapActivatingUserAsync(device, request.ActivatedBy, utcNow, cancellationToken);

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

    public async Task<DeviceResponse?> SetUsbBlockingAsync(
        Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetTrackedByIdAsync(id, cancellationToken);
        if (device is null)
        {
            return null;
        }

        device.UsbBlockingEnabled = enabled;
        await _deviceRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "USB blocking set to {Enabled} for device {DeviceId}.", enabled, device.DeviceId);

        return MapToResponse(device);
    }

    public async Task<DeviceResponse?> SetStoreGatingAsync(
        Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetTrackedByIdAsync(id, cancellationToken);
        if (device is null)
        {
            return null;
        }

        device.StoreGatingEnabled = enabled;
        await _deviceRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Microsoft Store gating set to {Enabled} for device {DeviceId}.", enabled, device.DeviceId);

        return MapToResponse(device);
    }

    /// <summary>
    /// Links the device to the EMS user that activated it (by the employee code
    /// the agent reports from its local activation state). The password was
    /// already verified when the user activated via the login window; here we
    /// just record who it was. ActivatedAt is stamped once, on first mapping.
    /// </summary>
    private async Task MapActivatingUserAsync(
        Device device, string? activatedBy, DateTime utcNow, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(activatedBy))
        {
            return;
        }

        var user = await _userRepository.GetByLoginIdentifierAsync(activatedBy.Trim(), cancellationToken);
        if (user is null)
        {
            _logger.LogWarning(
                "Device {DeviceId} reported activation by '{ActivatedBy}', but no such user exists.",
                device.DeviceId, activatedBy);
            return;
        }

        if (device.ActivatedByUserId == user.Id)
        {
            return; // Already mapped to this user.
        }

        device.ActivatedByUserId = user.Id;
        device.ActivatedAt ??= utcNow;

        _logger.LogInformation(
            "Device {DeviceId} mapped to user {Username} ({EmployeeCode}).",
            device.DeviceId, user.Username, user.EmployeeCode);
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
            LastHeartbeatTime = device.LastHeartbeatTime,
            UsbBlockingEnabled = device.UsbBlockingEnabled,
            StoreGatingEnabled = device.StoreGatingEnabled,
            ActivatedByUserId = device.ActivatedByUserId,
            ActivatedByEmployeeCode = device.ActivatedByUser?.EmployeeCode,
            ActivatedByName = device.ActivatedByUser?.Username,
            ActivatedByEmail = device.ActivatedByUser?.Email,
            ActivatedAt = device.ActivatedAt
        };
    }
}
