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

    /// <summary>The device's API credential; created at first registration.</summary>
    public DeviceAuthentication? Authentication { get; set; }
}
