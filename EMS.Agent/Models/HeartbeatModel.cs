namespace EMS.Agent.Models;

/// <summary>
/// Lightweight liveness payload; mirrors the EMS.API heartbeat contract.
/// </summary>
public class HeartbeatModel
{
    public string? IPAddress { get; set; }

    public string? Username { get; set; }

    public string? AgentVersion { get; set; }
}
