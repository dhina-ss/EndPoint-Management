namespace EMS.API.Entities;

/// <summary>
/// Cumulative network data usage for one device on one calendar day (UTC).
/// The agent reports byte deltas with each heartbeat; each report increments
/// the running totals rather than inserting a new row, so the table stays at
/// one row per device/day.
/// </summary>
public class NetworkUsageRecord
{
    public Guid Id { get; set; }

    /// <summary>Foreign key to <see cref="Device.Id"/>.</summary>
    public Guid DeviceId { get; set; }

    public Device Device { get; set; } = null!;

    /// <summary>Calendar day (UTC) these totals belong to.</summary>
    public DateOnly UsageDate { get; set; }

    /// <summary>Total bytes uploaded on this day.</summary>
    public long BytesSent { get; set; }

    /// <summary>Total bytes downloaded on this day.</summary>
    public long BytesReceived { get; set; }

    public DateTime LastUpdated { get; set; }
}
