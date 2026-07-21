namespace EMS.Agent.Models;

/// <summary>
/// Accumulated foreground time for one application since the last upload.
/// </summary>
public class AppUsageModel
{
    public string ApplicationName { get; set; } = string.Empty;

    public int DurationSeconds { get; set; }
}
