namespace EMS.API.Configuration;

/// <summary>
/// Database behavior options bound from the "DatabaseSettings" configuration section.
/// </summary>
public class DatabaseSettings
{
    public const string SectionName = "DatabaseSettings";

    /// <summary>Number of automatic retries on transient PostgreSQL failures.</summary>
    public int MaxRetryCount { get; set; } = 5;

    /// <summary>Upper bound for the delay between retries, in seconds.</summary>
    public int MaxRetryDelaySeconds { get; set; } = 10;

    /// <summary>Command timeout in seconds.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>Enables logging of parameter values. Development only.</summary>
    public bool EnableSensitiveDataLogging { get; set; }
}
