using System.Runtime.Versioning;
using Microsoft.Win32;

namespace EMS.Agent.Helpers;

/// <summary>
/// Enables or disables Microsoft Store installs via machine policy. Writing
/// under HKLM\SOFTWARE\Policies requires admin/SYSTEM, so this only works from
/// the agent service, not the per-user unlock window.
///
/// Two policies are toggled together:
///   1. WindowsStore\RemoveWindowsStore ("Turn off the Store application").
///      Fully blocks the Store, but Microsoft only honors it on Enterprise
///      and Education editions - it is silently ignored on Windows Pro/Home.
///   2. Appx\BlockNonAdminUserInstall ("Prevent non-admin users from
///      installing packaged Windows apps"). This IS honored on Pro/Home and
///      stops a standard (non-admin) user from installing anything from the
///      Store, which is the actual goal. Local administrators are unaffected
///      (they can install regardless), but managed endpoints run as standard
///      users, so this is the effective lever on Pro.
/// Setting both means the block works across every edition.
/// </summary>
[SupportedOSPlatform("windows")]
public static class StoreControlHelper
{
    private const string StorePolicyPath = @"SOFTWARE\Policies\Microsoft\WindowsStore";
    private const string StorePolicyValue = "RemoveWindowsStore";

    private const string AppxPolicyPath = @"SOFTWARE\Policies\Microsoft\Windows\Appx";
    private const string AppxPolicyValue = "BlockNonAdminUserInstall";

    /// <summary>Blocks Store installs (policies = 1).</summary>
    public static void DisableStore(ILogger logger) => Apply(1, logger);

    /// <summary>Allows Store installs again (policies = 0).</summary>
    public static void EnableStore(ILogger logger) => Apply(0, logger);

    private static void Apply(int value, ILogger logger)
    {
        var changed = false;
        changed |= SetPolicy(StorePolicyPath, StorePolicyValue, value, logger);
        changed |= SetPolicy(AppxPolicyPath, AppxPolicyValue, value, logger);

        if (changed)
        {
            logger.LogInformation(
                "Microsoft Store installs {State}.", value == 1 ? "blocked" : "allowed");
        }
    }

    /// <summary>Writes one policy value; returns true if it actually changed.</summary>
    private static bool SetPolicy(string path, string valueName, int value, ILogger logger)
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(path, writable: true);
            if (key is null)
            {
                logger.LogWarning("Could not open policy key {Path}; Store gating not fully applied.", path);
                return false;
            }

            if (key.GetValue(valueName) is int current && current == value)
            {
                return false; // Already in the desired state; avoid needless writes.
            }

            key.SetValue(valueName, value, RegistryValueKind.DWord);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "No permission to change {Value}; this must run as the SYSTEM service.", valueName);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to set the {Value} policy.", valueName);
            return false;
        }
    }
}
