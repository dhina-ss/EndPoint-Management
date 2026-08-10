namespace EMS.API.Entities;

/// <summary>
/// One threat that Microsoft Defender has recorded on a device. The agent
/// replaces the whole set each report, so rows reflect Defender's current
/// threat history.
/// </summary>
public class DeviceThreat
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    public Device Device { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>Defender SeverityID: 1 Low, 2 Moderate, 4 High, 5 Severe.</summary>
    public int? Severity { get; set; }

    public DateTime? DetectedAt { get; set; }

    /// <summary>True once Defender has quarantined/removed it.</summary>
    public bool Remediated { get; set; }

    public string? Resource { get; set; }

    public DateTime ReportedAt { get; set; }
}
