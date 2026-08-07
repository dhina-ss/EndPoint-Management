using EMS.API.DTOs;

namespace EMS.API.Services;

public interface IDeviceService
{
    Task<DeviceRegisterResponse> RegisterAsync(DeviceRegisterRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<DeviceResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Updates the device's USB mass-storage blocking policy. Returns null if the device does not exist.</summary>
    Task<DeviceResponse?> SetUsbBlockingAsync(Guid id, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>Updates the device's Microsoft Store gating policy. Returns null if the device does not exist.</summary>
    Task<DeviceResponse?> SetStoreGatingAsync(Guid id, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a precise GPS fix reported by the agent (device identified by its
    /// external device-id string). Returns false if the device does not exist.
    /// </summary>
    Task<bool> SetGpsLocationAsync(
        string deviceId, double latitude, double longitude, double accuracyMeters,
        CancellationToken cancellationToken = default);
}
