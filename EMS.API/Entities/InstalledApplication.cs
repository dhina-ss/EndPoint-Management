namespace EMS.API.Entities;

/// <summary>
/// One application present on a device, as last reported by its agent. The
/// agent replaces the whole set each inventory cycle, so rows here always
/// reflect the most recent scan.
/// </summary>
public class InstalledApplication
{
    public Guid Id { get; set; }

    /// <summary>Foreign key to <see cref="Device.Id"/>.</summary>
    public Guid DeviceId { get; set; }

    public Device Device { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string? Version { get; set; }

    public string? Publisher { get; set; }

    /// <summary>Main executable, e.g. "chrome.exe"; null when undetermined.</summary>
    public string? ExecutableName { get; set; }

    /// <summary>True for Microsoft Store / built-in UWP apps.</summary>
    public bool IsStoreApp { get; set; }

    public DateTime ReportedAt { get; set; }
}
