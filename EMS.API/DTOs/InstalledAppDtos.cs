namespace EMS.API.DTOs;

/// <summary>
/// One application in the agent's inventory report. Deliberately unvalidated:
/// a single over-length field (e.g. a Store app whose Vendor is a long
/// certificate subject) must never reject the whole batch with a 400 and leave
/// the inventory stale. The service sanitizes and truncates on ingestion.
/// </summary>
public class InstalledAppDto
{
    public string Name { get; set; } = string.Empty;

    public string? Version { get; set; }

    public string? Publisher { get; set; }

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
