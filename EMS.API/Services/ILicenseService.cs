using EMS.API.DTOs;

namespace EMS.API.Services;

/// <summary>
/// Validates tenant license keys during agent activation. The tenants table is
/// owned by the portal (The Lance) and shared via the same database.
/// </summary>
public interface ILicenseService
{
    Task<LicenseValidateResponse> ValidateAsync(
        string licenseKey, CancellationToken cancellationToken = default);
}
