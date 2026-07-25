namespace EMS.API.DTOs;

/// <summary>
/// Read model returned for device queries.
/// </summary>
public class DeviceResponse
{
    public Guid Id { get; set; }

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

    public DateTime? LastHeartbeatTime { get; set; }

    public bool UsbBlockingEnabled { get; set; }

    public bool StoreGatingEnabled { get; set; }
}
