namespace EMS.API.Entities;

/// <summary>What a queued command asks the agent to do.</summary>
public enum DeviceCommandType
{
    /// <summary>Silently uninstall an app already present on the device.</summary>
    Uninstall = 0,

    /// <summary>Deliver an installer package and run it silently.</summary>
    Install = 1,

    /// <summary>
    /// Deliver a newer installer package over an existing app. Mechanically
    /// identical to <see cref="Install"/>; kept distinct for dashboard intent.
    /// </summary>
    Update = 2
}

/// <summary>Lifecycle of a queued command.</summary>
public enum DeviceCommandStatus
{
    /// <summary>Queued; not yet handed to the device.</summary>
    Pending = 0,

    /// <summary>Delivered to the agent, which is (or will be) running it.</summary>
    Dispatched = 1,

    /// <summary>The agent ran it and reported success.</summary>
    Succeeded = 2,

    /// <summary>The agent ran it and reported failure (see ResultMessage).</summary>
    Failed = 3
}

/// <summary>
/// One software-management action targeted at a device. The dashboard enqueues
/// it, the agent drains it on its command-poll cycle, runs it silently, then
/// reports the outcome back.
/// </summary>
public class DeviceCommand
{
    public Guid Id { get; set; }

    /// <summary>Foreign key to <see cref="Device.Id"/>.</summary>
    public Guid DeviceId { get; set; }

    public Device Device { get; set; } = null!;

    public DeviceCommandType Type { get; set; }

    public DeviceCommandStatus Status { get; set; }

    // --- Uninstall / Update target (the app to act on) ---
    public string? TargetAppName { get; set; }

    public string? TargetAppVersion { get; set; }

    /// <summary>True when the target is a Microsoft Store / UWP app.</summary>
    public bool TargetIsStoreApp { get; set; }

    // --- Install / Update payload (the package to run) ---
    public Guid? PackageId { get; set; }

    public InstallerPackage? Package { get; set; }

    // --- Result ---
    public string? ResultMessage { get; set; }

    /// <summary>Process exit code the agent reported, when applicable.</summary>
    public int? ResultCode { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? DispatchedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
