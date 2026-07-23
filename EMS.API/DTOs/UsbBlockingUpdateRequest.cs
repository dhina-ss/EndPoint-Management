namespace EMS.API.DTOs;

/// <summary>
/// Request body for toggling a device's USB mass-storage blocking policy.
/// </summary>
public class UsbBlockingUpdateRequest
{
    public bool Enabled { get; set; }
}
