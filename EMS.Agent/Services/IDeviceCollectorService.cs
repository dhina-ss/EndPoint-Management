using EMS.Agent.Models;

namespace EMS.Agent.Services;

/// <summary>
/// Collects the hardware/OS inventory of the local machine.
/// </summary>
public interface IDeviceCollectorService
{
    Task<DeviceInventoryModel> CollectAsync(CancellationToken cancellationToken = default);
}
