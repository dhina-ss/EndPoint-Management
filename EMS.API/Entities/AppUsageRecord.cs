namespace EMS.API.Entities;

/// <summary>
/// Cumulative foreground-usage time for one application, on one device, on
/// one calendar day (UTC). The agent reports deltas periodically; each
/// report increments DurationSeconds rather than inserting a new row, so
/// the table stays at one row per device/app/day regardless of uptime.
/// </summary>
public class AppUsageRecord
{
    public Guid Id { get; set; }

    /// <summary>Foreign key to <see cref="Device.Id"/>.</summary>
    public Guid DeviceId { get; set; }

    public Device Device { get; set; } = null!;

    /// <summary>Process name of the foreground application, e.g. "chrome", "EXCEL".</summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>Calendar day (UTC) this usage total belongs to.</summary>
    public DateOnly UsageDate { get; set; }

    /// <summary>Cumulative foreground time for this app on this day.</summary>
    public int DurationSeconds { get; set; }

    public DateTime LastUpdated { get; set; }
}
