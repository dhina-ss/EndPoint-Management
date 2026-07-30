namespace EMS.API.Entities;

/// <summary>
/// Total working time for one device on one device-local calendar day. The
/// agent reports deltas (time while logged in, awake and unlocked); each report
/// increments <see cref="WorkedSeconds"/>, keeping one row per device/day.
/// </summary>
public class WorkSessionRecord
{
    public Guid Id { get; set; }

    /// <summary>Foreign key to <see cref="Device.Id"/>.</summary>
    public Guid DeviceId { get; set; }

    public Device Device { get; set; } = null!;

    /// <summary>Device-local calendar day this total belongs to.</summary>
    public DateOnly WorkDate { get; set; }

    /// <summary>Cumulative working seconds on this day.</summary>
    public int WorkedSeconds { get; set; }

    public DateTime LastUpdated { get; set; }
}
