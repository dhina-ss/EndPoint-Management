using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace EMS.Agent.Helpers;

/// <summary>
/// Blocks applications from launching using Image File Execution Options
/// (IFEO): setting a "Debugger" value for an executable makes Windows launch
/// that debugger instead of the program. Pointing it at systray.exe (which
/// exits immediately) means the blocked app simply never starts.
///
/// Any already-running instance is also terminated, so a block applied while
/// the app is open takes effect immediately rather than at next launch.
///
/// Only keys this agent created are ever removed - each carries an
/// EMSBlocked marker value - so IFEO entries belonging to debuggers or other
/// software are left untouched.
/// </summary>
[SupportedOSPlatform("windows")]
public static class AppBlockingHelper
{
    private const string IfeoPath =
        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
    private const string MarkerValue = "EMSBlocked";
    private const string DebuggerValue = "Debugger";

    /// <summary>
    /// Executables that must never be blocked: doing so would make the
    /// machine unusable or cut off the agent itself. This guard applies
    /// regardless of what the server sends.
    /// </summary>
    private static readonly HashSet<string> ProtectedExecutables = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe", "winlogon.exe", "csrss.exe", "lsass.exe", "services.exe",
        "svchost.exe", "smss.exe", "wininit.exe", "dwm.exe", "logonui.exe",
        "fontdrvhost.exe", "sihost.exe", "ctfmon.exe", "runtimebroker.exe",
        "userinit.exe", "spoolsv.exe", "conhost.exe", "system", "registry",
        "ems.agent.exe",
    };

    public static void ApplyPolicy(IEnumerable<string> blockedExecutables, ILogger logger)
    {
        var desired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in blockedExecutables)
        {
            var exe = Normalize(raw);
            if (exe is null)
            {
                continue;
            }

            if (ProtectedExecutables.Contains(exe))
            {
                logger.LogWarning(
                    "Refusing to block {Executable}: it is a protected system process.", exe);
                continue;
            }

            desired.Add(exe);
        }

        try
        {
            SyncIfeoEntries(desired, logger);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to apply application blocking policy.");
        }

        TerminateBlocked(desired, logger);
    }

    private static void SyncIfeoEntries(HashSet<string> desired, ILogger logger)
    {
        using var ifeo = Registry.LocalMachine.OpenSubKey(IfeoPath, writable: true);
        if (ifeo is null)
        {
            logger.LogWarning("IFEO registry key unavailable; cannot apply application blocking.");
            return;
        }

        // Remove blocks we previously created that are no longer wanted.
        foreach (var existing in ifeo.GetSubKeyNames())
        {
            if (desired.Contains(existing))
            {
                continue;
            }

            try
            {
                using var candidate = ifeo.OpenSubKey(existing);
                if (candidate?.GetValue(MarkerValue) is null)
                {
                    // Not ours - leave it alone.
                    continue;
                }
            }
            catch
            {
                continue;
            }

            try
            {
                ifeo.DeleteSubKeyTree(existing, throwOnMissingSubKey: false);
                logger.LogInformation("Application {Executable} unblocked.", existing);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not remove block for {Executable}.", existing);
            }
        }

        // Add or refresh the blocks we do want.
        var blocker = Path.Combine(Environment.SystemDirectory, "systray.exe");
        foreach (var exe in desired)
        {
            try
            {
                using var key = ifeo.CreateSubKey(exe, writable: true);
                if (key is null)
                {
                    continue;
                }

                if (key.GetValue(DebuggerValue) as string == blocker)
                {
                    continue;
                }

                key.SetValue(DebuggerValue, blocker, RegistryValueKind.String);
                key.SetValue(MarkerValue, 1, RegistryValueKind.DWord);
                logger.LogInformation("Application {Executable} blocked.", exe);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not apply block for {Executable}.", exe);
            }
        }
    }

    /// <summary>
    /// IFEO only stops future launches, so anything already running when the
    /// block arrives is closed here.
    /// </summary>
    private static void TerminateBlocked(HashSet<string> desired, ILogger logger)
    {
        if (desired.Count == 0)
        {
            return;
        }

        foreach (var exe in desired)
        {
            var processName = Path.GetFileNameWithoutExtension(exe);
            if (string.IsNullOrEmpty(processName))
            {
                continue;
            }

            Process[] running;
            try
            {
                running = Process.GetProcessesByName(processName);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not enumerate processes named {Process}.", processName);
                continue;
            }

            foreach (var process in running)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    logger.LogInformation("Terminated blocked application {Executable}.", exe);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Could not terminate {Executable}.", exe);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
    }

    private static string? Normalize(string? raw)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        // Guard against a path being sent where a file name is expected; the
        // IFEO key name must be the bare executable name.
        value = Path.GetFileName(value);

        if (!value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            value += ".exe";
        }

        return value;
    }
}
