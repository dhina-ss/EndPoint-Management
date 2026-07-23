namespace EMS.API.Entities;

/// <summary>
/// A domain that should be blocked on a specific device, in addition to the
/// always-on default phishing/malware blocklist baked into the agent. The
/// agent enforces this by adding the domain to the Windows hosts file, which
/// is consulted before DNS on every network.
/// </summary>
public class BlockedWebsite
{
    public Guid Id { get; set; }

    /// <summary>Foreign key to <see cref="Device.Id"/>.</summary>
    public Guid DeviceId { get; set; }

    public Device Device { get; set; } = null!;

    /// <summary>Bare host to block, normalized (lowercase, no scheme/path), e.g. "example.com".</summary>
    public string Domain { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }
}
