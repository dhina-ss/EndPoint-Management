namespace EMS.API.DTOs;

/// <summary>One threat in the agent's Defender report.</summary>
public class ThreatDto
{
    public string Name { get; set; } = string.Empty;
    public int? Severity { get; set; }
    public DateTime? DetectedAt { get; set; }
    public bool Remediated { get; set; }
    public string? Resource { get; set; }
}

/// <summary>Microsoft Defender status + threat snapshot reported by an agent.</summary>
public class SecurityStatusReportRequest
{
    public bool? RealtimeProtectionEnabled { get; set; }
    public bool? AntivirusEnabled { get; set; }
    public DateTime? LastQuickScan { get; set; }
    public DateTime? LastFullScan { get; set; }
    public string? SignatureVersion { get; set; }
    public int? SignatureAgeDays { get; set; }
    public string? EngineVersion { get; set; }
    public List<ThreatDto> Threats { get; set; } = new();
}

/// <summary>A recorded threat, for the dashboard.</summary>
public class ThreatResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Severity { get; set; }
    public DateTime? DetectedAt { get; set; }
    public bool Remediated { get; set; }
    public string? Resource { get; set; }
}
