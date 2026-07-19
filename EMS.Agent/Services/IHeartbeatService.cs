namespace EMS.Agent.Services;

/// <summary>
/// Builds and sends one liveness heartbeat.
/// </summary>
public interface IHeartbeatService
{
    Task<bool> SendHeartbeatAsync(CancellationToken cancellationToken = default);
}
