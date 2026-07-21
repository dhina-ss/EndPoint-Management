namespace EMS.API.DTOs;

/// <summary>
/// Read model for a device's per-application usage on a given day.
/// </summary>
public class AppUsageSummaryResponse
{
    public string ApplicationName { get; set; } = string.Empty;

    public int DurationSeconds { get; set; }

    public DateOnly UsageDate { get; set; }
}
