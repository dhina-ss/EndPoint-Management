namespace EMS.API.Entities;

/// <summary>
/// Per-device API credential. Holds only the SHA-256 hash of the token —
/// the raw token is returned to the agent once, at registration, and is
/// never stored or logged.
/// </summary>
public class DeviceAuthentication
{
    public Guid Id { get; set; }

    /// <summary>Foreign key to <see cref="Device.Id"/> (one credential per device).</summary>
    public Guid DeviceId { get; set; }

    public Device Device { get; set; } = null!;

    /// <summary>Base64-encoded SHA-256 hash of the device token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }

    public DateTime? LastUsedDate { get; set; }

    public bool IsActive { get; set; }
}
