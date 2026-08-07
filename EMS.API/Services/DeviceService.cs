using EMS.API.DTOs;
using EMS.API.Entities;
using EMS.API.Repositories;

namespace EMS.API.Services;

public class DeviceService : IDeviceService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceAuthService _deviceAuthService;
    private readonly IUserRepository _userRepository;
    private readonly IGeoLocationService _geoLocationService;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(
        IDeviceRepository deviceRepository,
        IDeviceAuthService deviceAuthService,
        IUserRepository userRepository,
        IGeoLocationService geoLocationService,
        ILogger<DeviceService> logger)
    {
        _deviceRepository = deviceRepository;
        _deviceAuthService = deviceAuthService;
        _userRepository = userRepository;
        _geoLocationService = geoLocationService;
        _logger = logger;
    }

    public async Task<bool> SetGpsLocationAsync(
        string deviceId, double latitude, double longitude, double accuracyMeters,
        CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetByDeviceIdAsync(deviceId, cancellationToken);
        if (device is null)
        {
            return false;
        }

        device.GpsLatitude = latitude;
        device.GpsLongitude = longitude;
        device.GpsAccuracyMeters = accuracyMeters;
        device.GpsUpdatedAt = DateTime.UtcNow;

        // Reverse-geocode to a readable city/country (best-effort).
        var place = await _geoLocationService.ReverseAsync(latitude, longitude, cancellationToken);
        if (place is not null)
        {
            device.GpsCity = place.Value.City;
            device.GpsCountry = place.Value.Country;
        }

        await _deviceRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Device {DeviceId} GPS location updated: {City}, {Country}.", deviceId, device.GpsCity, device.GpsCountry);
        return true;
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
        var utcNow = DateTime.UtcNow;
        var response = new DeviceResponse
        {
            Status = DeviceStatusCalculator.Compute(device.LastHeartbeatTime, device.SuspendedAt, utcNow),
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
            ActivatedAt = device.ActivatedAt,
            PublicIPAddress = device.PublicIPAddress,
            LocationUpdatedAt = device.LocationUpdatedAt
        };

        // Effective location: prefer a recent GPS fix (precise, VPN-proof);
        // otherwise fall back to the approximate IP location.
        var gpsFresh = device.GpsLatitude is not null
            && device.GpsUpdatedAt is not null
            && utcNow - device.GpsUpdatedAt.Value < TimeSpan.FromHours(6);

        if (gpsFresh)
        {
            response.LocationSource = "GPS";
            response.Latitude = device.GpsLatitude;
            response.Longitude = device.GpsLongitude;
            response.GpsAccuracyMeters = device.GpsAccuracyMeters;
            response.LocationCity = device.GpsCity ?? device.LocationCity;
            response.LocationCountry = device.GpsCountry ?? device.LocationCountry;
            response.LocationRegion = device.LocationRegion;
        }
        else if (device.Latitude is not null)
        {
            response.LocationSource = "IP";
            response.Latitude = device.Latitude;
            response.Longitude = device.Longitude;
            response.LocationCity = device.LocationCity;
            response.LocationRegion = device.LocationRegion;
            response.LocationCountry = device.LocationCountry;
        }

        return response;
    }
}
