namespace EMS.Agent.Models;

/// <summary>One threat known to Microsoft Defender on this device.</summary>
public class SecurityThreatModel
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Defender SeverityID: 1 Low, 2 Moderate, 4 High, 5 Severe.</summary>
    public int? Severity { get; set; }

    public DateTime? DetectedAt { get; set; }

    /// <summary>True once Defender has remediated (quarantined/removed) it.</summary>
    public bool Remediated { get; set; }

    /// <summary>Affected file(s)/resource path.</summary>
    public string? Resource { get; set; }
}

/// <summary>Microsoft Defender status snapshot + threat history for a device.</summary>
public class SecurityStatusModel
{
    public bool? RealtimeProtectionEnabled { get; set; }
    public bool? AntivirusEnabled { get; set; }
    public DateTime? LastQuickScan { get; set; }
    public DateTime? LastFullScan { get; set; }
    public string? SignatureVersion { get; set; }
    public int? SignatureAgeDays { get; set; }
    public string? EngineVersion { get; set; }
    public List<SecurityThreatModel> Threats { get; set; } = new();
}
