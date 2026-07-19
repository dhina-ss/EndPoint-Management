using EMS.API.Entities;

namespace EMS.API.Services;

public interface IDeviceAuthService
{
    /// <summary>
    /// Issues a fresh token for the device, replacing any previous credential,
    /// and returns the raw token (the only time it is ever visible).
    /// </summary>
    Task<string> IssueTokenAsync(Device device, CancellationToken cancellationToken = default);
}
