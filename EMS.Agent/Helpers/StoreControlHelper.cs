using System.Runtime.Versioning;
using Microsoft.Win32;

namespace EMS.Agent.Helpers;

/// <summary>
/// Enables or disables the Microsoft Store via the documented
/// "Turn off the Store application" machine policy
/// (HKLM\SOFTWARE\Policies\Microsoft\WindowsStore\RemoveWindowsStore).
/// Writing under HKLM\SOFTWARE\Policies requires admin/SYSTEM, so this only
/// works from the agent service, not the per-user unlock window.
/// </summary>
[SupportedOSPlatform("windows")]
public static class StoreControlHelper
{
    private const string PolicyPath = @"SOFTWARE\Policies\Microsoft\WindowsStore";
    private const string PolicyValue = "RemoveWindowsStore";

    /// <summary>Disables the Store (RemoveWindowsStore = 1).</summary>
    public static void DisableStore(ILogger logger) => SetPolicy(1, logger);

    /// <summary>Re-enables the Store (RemoveWindowsStore = 0).</summary>
    public static void EnableStore(ILogger logger) => SetPolicy(0, logger);

    private static void SetPolicy(int value, ILogger logger)
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(PolicyPath, writable: true);
            if (key is null)
            {
                logger.LogWarning("Could not open the Windows Store policy key; Store gating not applied.");
                return;
            }

            if (key.GetValue(PolicyValue) is int current && current == value)
            {
                return; // Already in the desired state; avoid needless writes.
            }

            key.SetValue(PolicyValue, value, RegistryValueKind.DWord);
            logger.LogInformation("Microsoft Store {State}.", value == 1 ? "disabled" : "enabled");
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "No permission to change the Store policy; this must run as the SYSTEM service.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to set the Microsoft Store policy.");
        }
    }
}
