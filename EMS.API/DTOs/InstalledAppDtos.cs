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

/// <summary>Installed application as shown in the dashboard (read-only).</summary>
public class InstalledAppResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Version { get; set; }

    public string? Publisher { get; set; }

    public string? ExecutableName { get; set; }

    public bool IsStoreApp { get; set; }
}
