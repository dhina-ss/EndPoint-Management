using System.ComponentModel.DataAnnotations;

namespace EMS.API.DTOs;

/// <summary>
/// Heartbeat payload sent by the agent. The device identity comes from the
/// X-Device-Id / X-Device-Token headers, not the body.
/// </summary>
public class HeartbeatRequest
{
    [MaxLength(45)]
    public string? IPAddress { get; set; }

    [MaxLength(100)]
    public string? Username { get; set; }

    [MaxLength(50)]
    public string? AgentVersion { get; set; }

    /// <summary>Live resource snapshot; omitted by agents without live monitoring.</summary>
    public SystemMetricsPayload? Metrics { get; set; }
}
