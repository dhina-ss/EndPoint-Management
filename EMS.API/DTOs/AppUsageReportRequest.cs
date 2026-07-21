using System.ComponentModel.DataAnnotations;

namespace EMS.API.DTOs;

/// <summary>
/// One application's accumulated foreground time since the agent's last report.
/// </summary>
public class AppUsageEntryDto
{
    [Required]
    [MaxLength(200)]
    public string ApplicationName { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int DurationSeconds { get; set; }
}

/// <summary>
/// Batch of per-application usage deltas reported by the agent.
/// </summary>
public class AppUsageReportRequest
{
    public List<AppUsageEntryDto> UsageRecords { get; set; } = new();
}
