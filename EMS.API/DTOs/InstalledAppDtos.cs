using System.ComponentModel.DataAnnotations;

namespace EMS.API.DTOs;

/// <summary>One application in the agent's inventory report.</summary>
public class InstalledAppDto
{
    [Required]
    [MaxLength(300)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Version { get; set; }

    [MaxLength(200)]
    public string? Publisher { get; set; }

    [MaxLength(260)]
    public string? ExecutableName { get; set; }

    public bool IsStoreApp { get; set; }
}

/// <summary>Full installed-application inventory reported by an agent.</summary>
public class InstalledAppsReportRequest
{
    public List<InstalledAppDto> Applications { get; set; } = new();
}

/// <summary>Installed application as shown in the dashboard.</summary>
public class InstalledAppResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Version { get; set; }

    public string? Publisher { get; set; }

    public string? ExecutableName { get; set; }

    public bool IsStoreApp { get; set; }

    /// <summary>True when this app is currently blocked on the device.</summary>
    public bool IsBlocked { get; set; }

    /// <summary>
    /// False when the app has no resolvable executable (typically Store
    /// apps), so the dashboard can explain why blocking is unavailable.
    /// </summary>
    public bool CanBlock { get; set; }
}

/// <summary>Request body for blocking an application on a device.</summary>
public class BlockApplicationRequest
{
    [Required]
    [MaxLength(260)]
    public string ExecutableName { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? DisplayName { get; set; }
}
