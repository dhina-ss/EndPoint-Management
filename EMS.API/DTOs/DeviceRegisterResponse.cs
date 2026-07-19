namespace EMS.API.DTOs;

/// <summary>
/// Result of a device registration attempt.
/// </summary>
public class DeviceRegisterResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? DeviceId { get; set; }

    /// <summary>
    /// API token for this device, issued on every successful registration.
    /// The agent must store it and send it as X-Device-Token on protected calls.
    /// </summary>
    public string? Token { get; set; }
}
