using System.ComponentModel.DataAnnotations;

namespace EMS.API.DTOs;

/// <summary>
/// Payload sent by the endpoint agent to register (or re-register) a device.
/// </summary>
public class DeviceRegisterRequest
{
    [Required(ErrorMessage = "DeviceId is required.")]
    [MaxLength(100, ErrorMessage = "DeviceId cannot exceed 100 characters.")]
    public string DeviceId { get; set; } = string.Empty;

    [Required(ErrorMessage = "DeviceName is required.")]
    [MaxLength(200)]
    public string DeviceName { get; set; } = string.Empty;

    [Required(ErrorMessage = "SerialNumber is required.")]
    [MaxLength(100)]
    public string SerialNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Manufacturer { get; set; }

    [MaxLength(100)]
    public string? Model { get; set; }

    [MaxLength(200)]
    public string? Processor { get; set; }

    [MaxLength(50)]
    public string? RamSize { get; set; }

    [MaxLength(50)]
    public string? StorageSize { get; set; }

    [MaxLength(100)]
    public string? OSVersion { get; set; }

    [MaxLength(50)]
    public string? OSBuildNumber { get; set; }

    [MaxLength(45)]
    public string? IPAddress { get; set; }

    [RegularExpression(
        @"^([0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}$",
        ErrorMessage = "MACAddress must be in the format XX-XX-XX-XX-XX-XX or XX:XX:XX:XX:XX:XX.")]
    public string? MACAddress { get; set; }

    [MaxLength(100)]
    public string? Username { get; set; }

    public DateTime? LastBootTime { get; set; }
}
