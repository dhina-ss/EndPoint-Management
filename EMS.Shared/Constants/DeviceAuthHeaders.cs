namespace EMS.Shared.Constants;

/// <summary>
/// HTTP header names for device authentication, shared by EMS.API
/// (validation) and EMS.Agent (sending) so they can never drift apart.
/// </summary>
public static class DeviceAuthHeaders
{
    public const string DeviceId = "X-Device-Id";

    public const string Token = "X-Device-Token";
}
