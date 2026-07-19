namespace EMS.API.DTOs;

/// <summary>
/// Authentication result shape, also used for 401 middleware responses.
/// </summary>
public class DeviceAuthResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? DeviceId { get; set; }

    public string? Token { get; set; }
}
