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

    /// <summary>Relative path of the user login endpoint (agent activation).</summary>
    public string LoginEndpoint { get; set; } = "/api/auth/login";

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

    /// <summary>Relative path of the app-usage report endpoint.</summary>
    public string AppUsageEndpoint { get; set; } = "/api/devices/app-usage";

    /// <summary>Relative path of the installed-applications report endpoint.</summary>
    public string InstalledAppsEndpoint { get; set; } = "/api/devices/installed-apps";

    /// <summary>How often the agent samples the foreground application, in seconds.</summary>
    public int AppUsageSampleIntervalSeconds { get; set; } = 20;

    /// <summary>How often accumulated app usage is uploaded, in minutes.</summary>
    public int AppUsageUploadIntervalMinutes { get; set; } = 5;
}
