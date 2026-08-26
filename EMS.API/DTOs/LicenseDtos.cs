using System.ComponentModel.DataAnnotations;

namespace EMS.API.DTOs;

/// <summary>Step-1 activation: the tenant license key the agent collected.</summary>
public class LicenseValidateRequest
{
    [Required]
    [MaxLength(100)]
    public string LicenseKey { get; set; } = string.Empty;
}

/// <summary>Result of validating a license key against the tenants table.</summary>
public class LicenseValidateResponse
{
    public bool Valid { get; set; }

    public string? Message { get; set; }

    public string? TenantId { get; set; }

    public string? TenantName { get; set; }
}
