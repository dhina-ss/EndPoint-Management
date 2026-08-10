using EMS.API.Entities;

namespace EMS.API.Repositories;

/// <summary>Storage for per-device Defender threat history.</summary>
public interface IDeviceThreatRepository
{
    Task<IReadOnlyList<DeviceThreat>> GetByDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>Replaces the device's whole threat set with a fresh report.</summary>
    Task ReplaceForDeviceAsync(
        Guid deviceId, IEnumerable<DeviceThreat> threats, CancellationToken cancellationToken = default);

    Task<int> CountActiveAsync(Guid deviceId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
