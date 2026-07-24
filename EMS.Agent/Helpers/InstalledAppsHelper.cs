using System.Management;
using System.Runtime.Versioning;
using EMS.Agent.Models;
using Microsoft.Win32;

namespace EMS.Agent.Helpers;

/// <summary>
/// Enumerates installed software from the two places Windows records it:
/// the Uninstall registry keys (traditional Win32/desktop apps) and the
/// Store program inventory (built-in and Store-installed UWP apps).
/// </summary>
[SupportedOSPlatform("windows")]
public static class InstalledAppsHelper
{
    private const string UninstallSubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string UninstallSubKeyWow = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    public static IReadOnlyList<InstalledAppModel> Collect(ILogger logger)
    {
        // Keyed by name+version so the same app found in several hives is
        // reported once.
        var apps = new Dictionary<string, InstalledAppModel>(StringComparer.OrdinalIgnoreCase);

        CollectDesktopApps(apps, logger);
        CollectStoreApps(apps, logger);

        return apps.Values
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void CollectDesktopApps(Dictionary<string, InstalledAppModel> apps, ILogger logger)
    {
        // Machine-wide installs, in both the 64-bit and 32-bit registry views.
        ReadUninstallKey(Registry.LocalMachine, UninstallSubKey, apps, logger);
        ReadUninstallKey(Registry.LocalMachine, UninstallSubKeyWow, apps, logger);

        // Per-user installs. The service runs as SYSTEM, so HKCU is SYSTEM's
        // own hive and would miss them; walk the loaded user hives instead.
        try
        {
            foreach (var sid in Registry.Users.GetSubKeyNames())
            {
                // Skip machine/service accounts and the _Classes side hives.
                if (!sid.StartsWith("S-1-5-21-", StringComparison.Ordinal) ||
                    sid.EndsWith("_Classes", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using var userHive = Registry.Users.OpenSubKey(sid);
                if (userHive is not null)
                {
                    ReadUninstallKey(userHive, UninstallSubKey, apps, logger);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not enumerate per-user installed applications.");
        }
    }

    private static void ReadUninstallKey(
        RegistryKey root, string subKeyPath, Dictionary<string, InstalledAppModel> apps, ILogger logger)
    {
        try
        {
            using var uninstallKey = root.OpenSubKey(subKeyPath);
            if (uninstallKey is null)
            {
                return;
            }

            foreach (var entryName in uninstallKey.GetSubKeyNames())
            {
                try
                {
                    using var entry = uninstallKey.OpenSubKey(entryName);
                    if (entry is null)
                    {
                        continue;
                    }

                    var name = entry.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    // SystemComponent=1 marks redistributables and hotfixes
                    // that Add/Remove Programs itself hides.
                    if (entry.GetValue("SystemComponent") is int systemComponent && systemComponent == 1)
                    {
                        continue;
                    }

                    var app = new InstalledAppModel
                    {
                        Name = name.Trim(),
                        Version = (entry.GetValue("DisplayVersion") as string)?.Trim(),
                        Publisher = (entry.GetValue("Publisher") as string)?.Trim(),
                        ExecutableName = ExtractExecutableName(entry.GetValue("DisplayIcon") as string),
                        IsStoreApp = false
                    };

                    apps[$"{app.Name}|{app.Version}"] = app;
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Skipped unreadable uninstall entry {Entry}.", entryName);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not read uninstall key {Path}.", subKeyPath);
        }
    }

    private static void CollectStoreApps(Dictionary<string, InstalledAppModel> apps, ILogger logger)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, Vendor, Version FROM Win32_InstalledStoreProgram");

            foreach (var found in searcher.Get())
            {
                using var obj = (ManagementObject)found;
                var name = obj["Name"] as string;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var app = new InstalledAppModel
                {
                    Name = name.Trim(),
                    Version = obj["Version"] as string,
                    Publisher = obj["Vendor"] as string,
                    // UWP apps have no stable single .exe to key IFEO on;
                    // they are listed for visibility, not blocking.
                    ExecutableName = null,
                    IsStoreApp = true
                };

                apps[$"{app.Name}|{app.Version}"] = app;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not enumerate Store applications.");
        }
    }

    /// <summary>
    /// DisplayIcon is usually the app's main executable, sometimes with an
    /// icon index suffix ("C:\App\app.exe,0") and/or quotes.
    /// </summary>
    private static string? ExtractExecutableName(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon))
        {
            return null;
        }

        var path = displayIcon.Trim().Trim('"');

        var commaIndex = path.LastIndexOf(',');
        if (commaIndex > 0)
        {
            path = path[..commaIndex];
        }

        path = path.Trim().Trim('"');

        if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            return Path.GetFileName(path);
        }
        catch
        {
            return null;
        }
    }
}
