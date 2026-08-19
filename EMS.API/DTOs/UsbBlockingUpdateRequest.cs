namespace EMS.API.DTOs;

/// <summary>
/// Request body for toggling a device's USB mass-storage blocking policy.
/// </summary>
public class UsbBlockingUpdateRequest
{
    public bool Enabled { get; set; }

    /// <summary>
    /// For USB blocking only: how long the block lasts, in minutes. Null (or 0)
    /// with Enabled=true means permanent. Ignored by the other policies that
    /// reuse this request. Ignored when Enabled=false.
    /// </summary>
    public int? DurationMinutes { get; set; }
}
