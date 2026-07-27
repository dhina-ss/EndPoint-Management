namespace EMS.Agent.Configuration;

/// <summary>
/// EMS server connection options bound from the "ApiSettings" configuration section.
/// </summary>
public class ApiSettings
{
    public const string SectionName = "ApiSettings";

    /// <summary>Base URL of the EMS.API server, e.g. https://localhost:7299.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Relative path of the device registration endpoint.</summary>
    public string RegisterEndpoint { get; set; } = "/api/devices/register";

    /// <summary>Relative path of the user login endpoint (agent activation and Store unlock).</summary>
    public string LoginEndpoint { get; set; } = "/api/auth/login";

    /// <summary>How long a Microsoft Store unlock lasts, in minutes.</summary>
    public int StoreUnlockMinutes { get; set; } = 15;

    /// <summary>Relative path of the heartbeat endpoint.</summary>
    public string HeartbeatEndpoint { get; set; } = "/api/devices/heartbeat";

    /// <summary>How often the agent sends a liveness heartbeat, in seconds.</summary>
    public int HeartbeatIntervalSeconds { get; set; } = 60;

    /// <summary>HTTP request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Total attempts per registration, including the first one.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Base delay between attempts; grows linearly with each attempt.</summary>
    public int RetryDelaySeconds { get; set; } = 5;

    /// <summary>How often the agent collects and reports inventory, in minutes.</summary>
    public int PollingIntervalMinutes { get; set; } = 10;

    /// <summary>
    /// How often the agent re-scans and reports the installed-application list,
    /// in minutes. Installed software changes rarely, so this runs on a slower
    /// cadence than the hardware inventory cycle.
    /// </summary>
    public int InstalledAppsIntervalMinutes { get; set; } = 60;

    /// <summary>Relative path of the app-usage report endpoint.</summary>
    public string AppUsageEndpoint { get; set; } = "/api/devices/app-usage";

    /// <summary>Relative path of the installed-applications report endpoint.</summary>
    public string InstalledAppsEndpoint { get; set; } = "/api/devices/installed-apps";

    /// <summary>How often the agent samples the foreground application, in seconds.</summary>
    public int AppUsageSampleIntervalSeconds { get; set; } = 20;

    /// <summary>How often accumulated app usage is uploaded, in minutes.</summary>
    public int AppUsageUploadIntervalMinutes { get; set; } = 5;

    /// <summary>Relative path the agent polls for pending software-management commands.</summary>
    public string PendingCommandsEndpoint { get; set; } = "/api/devices/commands/pending";

    /// <summary>Relative path the agent posts a command result to (append "/{id}/result").</summary>
    public string CommandResultEndpoint { get; set; } = "/api/devices/commands";

    /// <summary>Relative path the agent downloads an installer package from (append "/{id}/content").</summary>
    public string PackageContentEndpoint { get; set; } = "/api/packages";

    /// <summary>How often the agent polls for pending commands, in seconds.</summary>
    public int CommandPollIntervalSeconds { get; set; } = 30;

    /// <summary>Hard timeout for a single install/uninstall process, in minutes.</summary>
    public int CommandTimeoutMinutes { get; set; } = 10;
}
