using EMS.Agent.Models;

namespace EMS.Agent.Services;

/// <summary>
/// Produces a live resource snapshot for the current machine.
/// </summary>
public interface ISystemMetricsService
{
    SystemMetricsModel Collect();
}
