using System.Runtime.Versioning;
using EMS.Agent.Helpers;
using EMS.Agent.Models;

namespace EMS.Agent.Services;

/// <summary>
/// Builds the device inventory from WMI. Collection is best-effort per section:
/// a failing WMI class is logged and skipped so a partial inventory is still reported.
/// </summary>
[SupportedOSPlatform("windows")]
public class DeviceCollectorService : IDeviceCollectorService
{
    private readonly IDeviceIdService _deviceIdService;
    private readonly IActivationStore _activationStore;
    private readonly ILogger<DeviceCollectorService> _logger;

    public DeviceCollectorService(
        IDeviceIdService deviceIdService,
        IActivationStore activationStore,
        ILogger<DeviceCollectorService> logger)
    {
        _deviceIdService = deviceIdService;
        _activationStore = activationStore;
        _logger = logger;
    }

    public async Task<DeviceInventoryModel> CollectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Device inventory collection started.");

        var deviceId = await _deviceIdService.GetDeviceIdAsync(cancellationToken);

        // WMI has no async API; keep the caller's thread free.
        var inventory = await Task.Run(() => Collect(cancellationToken), cancellationToken);
        inventory.DeviceId = deviceId;

        // Who activated this device (verified at the login window); lets the
        // server map the device to that EMS user.
        inventory.ActivatedBy = _activationStore.ActivatedBy();

        _logger.LogInformation(
            "Device inventory collection completed. DeviceId: {DeviceId}, DeviceName: {DeviceName}",
            inventory.DeviceId, inventory.DeviceName);

        return inventory;
    }

    private DeviceInventoryModel Collect(CancellationToken cancellationToken)
    {
        var inventory = new DeviceInventoryModel
        {
            DeviceName = Environment.MachineName
        };

        CollectSection("computer system", () =>
        {
            var (manufacturer, model, totalMemoryBytes, loggedOnUser) = SystemInfoHelper.GetComputerSystemInfo();
            inventory.Manufacturer = manufacturer;
            inventory.Model = model;
            inventory.RamSize = totalMemoryBytes > 0 ? SystemInfoHelper.FormatBytesAsGigabytes(totalMemoryBytes) : null;

            // DOMAIN\user of the interactive session; null when running as a
            // service with no user logged on.
            inventory.Username = loggedOnUser;
        });

        cancellationToken.ThrowIfCancellationRequested();

        CollectSection("BIOS", () =>
        {
            inventory.SerialNumber = SystemInfoHelper.GetBiosSerialNumber() ?? string.Empty;
        });

        cancellationToken.ThrowIfCancellationRequested();

        CollectSection("processor", () =>
        {
            inventory.Processor = SystemInfoHelper.GetProcessorName();
        });

        cancellationToken.ThrowIfCancellationRequested();

        CollectSection("operating system", () =>
        {
            var (caption, buildNumber, lastBootTime) = SystemInfoHelper.GetOperatingSystemInfo();
            inventory.OSVersion = caption;
            inventory.OSBuildNumber = buildNumber;
            inventory.LastBootTime = lastBootTime;
        });

        cancellationToken.ThrowIfCancellationRequested();

        CollectSection("storage", () =>
        {
            var totalDiskBytes = SystemInfoHelper.GetTotalFixedDiskBytes();
            inventory.StorageSize = totalDiskBytes > 0 ? SystemInfoHelper.FormatBytesAsGigabytes(totalDiskBytes) : null;
        });

        cancellationToken.ThrowIfCancellationRequested();

        CollectSection("network", () =>
        {
            var (ipAddress, macAddress) = SystemInfoHelper.GetPrimaryNetworkInfo();
            inventory.IPAddress = ipAddress;
            inventory.MACAddress = macAddress;
        });

        ApplyFallbacks(inventory);
        return inventory;
    }

    /// <summary>
    /// The API requires SerialNumber; make sure a degraded WMI environment
    /// still produces a registrable inventory. DeviceId is guaranteed by
    /// <see cref="IDeviceIdService"/>.
    /// </summary>
    private static void ApplyFallbacks(DeviceInventoryModel inventory)
    {
        if (string.IsNullOrWhiteSpace(inventory.SerialNumber))
        {
            inventory.SerialNumber = "UNKNOWN";
        }

        if (string.IsNullOrWhiteSpace(inventory.Username))
        {
            inventory.Username = Environment.UserName;
        }
    }

    private void CollectSection(string section, Action collect)
    {
        try
        {
            collect();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect {Section} information.", section);
        }
    }
}
