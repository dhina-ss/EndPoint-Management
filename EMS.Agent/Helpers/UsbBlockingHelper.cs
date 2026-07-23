using System.Management;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace EMS.Agent.Helpers;

/// <summary>
/// Applies the USB mass-storage blocking policy. Only the USB mass-storage
/// driver (flash drives, external disks) is affected - keyboards, mice, and
/// other USB device classes are untouched.
///
/// Two layers, applied together:
///  - The driver's Start registry value governs devices connected from now
///    on (including after a reboot), the standard, Microsoft-documented
///    technique for this.
///  - Already-connected mass-storage devices are additionally
///    enabled/disabled directly via WMI for immediate effect, since the
///    registry value alone would not retroactively evict something already
///    mounted.
/// </summary>
[SupportedOSPlatform("windows")]
public static class UsbBlockingHelper
{
    private const string UsbStorRegistryPath = @"SYSTEM\CurrentControlSet\Services\UsbStor";
    private const int StartDisabled = 4;
    private const int StartManual = 3;

    public static void ApplyPolicy(bool blocked, ILogger logger)
    {
        SetStartupPolicy(blocked, logger);
        SetConnectedDevicesState(blocked, logger);
    }

    private static void SetStartupPolicy(bool blocked, ILogger logger)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(UsbStorRegistryPath, writable: true);
            if (key is null)
            {
                logger.LogWarning("USB storage driver registry key not found; cannot apply USB blocking policy.");
                return;
            }

            var desiredValue = blocked ? StartDisabled : StartManual;
            if (key.GetValue("Start") is int currentValue && currentValue == desiredValue)
            {
                return;
            }

            key.SetValue("Start", desiredValue, RegistryValueKind.DWord);
            logger.LogInformation("USB mass-storage policy set to {Policy}.", blocked ? "blocked" : "allowed");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to set USB storage registry policy.");
        }
    }

    private static void SetConnectedDevicesState(bool blocked, ILogger logger)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_PnPEntity WHERE Service = 'USBSTOR'");
            using var devices = searcher.Get();

            foreach (var found in devices)
            {
                using var device = (ManagementObject)found;
                try
                {
                    device.InvokeMethod(blocked ? "Disable" : "Enable", null);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Could not {Action} connected USB storage device {Name}.",
                        blocked ? "disable" : "enable", device["Name"]);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to enumerate connected USB storage devices.");
        }
    }
}
