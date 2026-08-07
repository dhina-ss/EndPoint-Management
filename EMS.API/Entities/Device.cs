namespace EMS.API.Entities;

/// <summary>
/// Persistence model for a managed endpoint device.
/// </summary>
public class Device
{
    public Guid Id { get; set; }

    /// <summary>Agent-generated unique identifier for the endpoint.</summary>
    public string DeviceId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string SerialNumber { get; set; } = string.Empty;

    public string? Manufacturer { get; set; }

    public string? Model { get; set; }

    public string? Processor { get; set; }

    public string? RamSize { get; set; }

    public string? StorageSize { get; set; }

    public string? OSVersion { get; set; }

    public string? OSBuildNumber { get; set; }

    public string? IPAddress { get; set; }

    public string? MACAddress { get; set; }

    public string? Username { get; set; }

    public DateTime? LastBootTime { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public DateTime LastSeen { get; set; }

    /// <summary>Time of the most recent heartbeat; null until the first one arrives.</summary>
    public DateTime? LastHeartbeatTime { get; set; }

    /// <summary>
    /// When true, the agent disables USB mass-storage (flash drives, external
    /// disks) on this device. Applied on the device's next heartbeat, not
    /// instantly. Other USB device classes (keyboard, mouse, etc.) are
    /// unaffected.
    /// </summary>
    public bool UsbBlockingEnabled { get; set; }

    /// <summary>
    /// When true, the agent keeps the Microsoft Store disabled on this device;
    /// a user must enter an EMS admin password (via the local unlock window)
    /// to temporarily re-enable it for installs. Applied on the next heartbeat.
    /// </summary>
    public bool StoreGatingEnabled { get; set; }

    /// <summary>The device's API credential; created at first registration.</summary>
    public DeviceAuthentication? Authentication { get; set; }

    /// <summary>
    /// The EMS user who activated this device (entered their employee code and
    /// password in the agent's activation window). Null until activated.
    /// </summary>
    public Guid? ActivatedByUserId { get; set; }

    public AppUser? ActivatedByUser { get; set; }

    /// <summary>When the device was first activated by that user.</summary>
    public DateTime? ActivatedAt { get; set; }

    /// <summary>
    /// Set when the agent signals the machine is suspending (sleep); cleared on
    /// the next heartbeat. Drives the "Sleep" device status.
    /// </summary>
    public DateTime? SuspendedAt { get; set; }

    // ---- Approximate location, resolved from the device's public IP ----

    /// <summary>The device's public (internet-facing) IP, seen on heartbeat.</summary>
    public string? PublicIPAddress { get; set; }

    public string? LocationCity { get; set; }

    public string? LocationRegion { get; set; }

    public string? LocationCountry { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    /// <summary>When the location was last resolved (on a public-IP change).</summary>
    public DateTime? LocationUpdatedAt { get; set; }

    // ---- Precise location, reported by the agent from Windows GPS/Wi-Fi ----

    public double? GpsLatitude { get; set; }

    public double? GpsLongitude { get; set; }

    public double? GpsAccuracyMeters { get; set; }

    public string? GpsCity { get; set; }

    public string? GpsCountry { get; set; }

    /// <summary>When the agent last reported a GPS fix.</summary>
    public DateTime? GpsUpdatedAt { get; set; }
}
