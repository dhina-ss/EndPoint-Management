namespace EMS.Agent.Models;

/// <summary>
/// Inventory snapshot collected from the local machine.
/// Mirrors the EMS.API device registration contract.
/// </summary>
public class DeviceInventoryModel
{
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
}
