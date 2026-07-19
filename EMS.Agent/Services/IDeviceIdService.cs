namespace EMS.Agent.Services;

/// <summary>
/// Provides the permanent unique identifier of this endpoint.
/// </summary>
public interface IDeviceIdService
{
    /// <summary>
    /// Returns the stored DeviceId, generating and persisting a new one on first run.
    /// </summary>
    Task<string> GetDeviceIdAsync(CancellationToken cancellationToken = default);
}
