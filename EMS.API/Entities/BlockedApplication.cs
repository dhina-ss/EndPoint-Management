namespace EMS.API.Entities;

/// <summary>
/// An application blocked from launching on a specific device. Keyed by
/// executable name because that is what the agent's enforcement (Image File
/// Execution Options) operates on. Kept separate from the installed-apps
/// inventory so a block survives the app being re-scanned or reinstalled.
/// </summary>
public class BlockedApplication
{
    public Guid Id { get; set; }

    /// <summary>Foreign key to <see cref="Device.Id"/>.</summary>
    public Guid DeviceId { get; set; }

    public Device Device { get; set; } = null!;

    /// <summary>Executable file name, normalized to lowercase, e.g. "chrome.exe".</summary>
    public string ExecutableName { get; set; } = string.Empty;

    /// <summary>Friendly name captured at block time, for display.</summary>
    public string? DisplayName { get; set; }

    public DateTime CreatedDate { get; set; }
}
