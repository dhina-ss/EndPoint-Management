using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using EMS.Agent.Models;
using Microsoft.Win32;

namespace EMS.Agent.Helpers;

/// <summary>
/// Silently uninstalls an installed application. Because the agent runs as
/// SYSTEM in Session 0 (no desktop), only unattended uninstalls are attempted:
/// an MSI product code (msiexec /x /qn) or a QuietUninstallString. An app whose
/// only uninstaller is interactive is reported as a failure rather than run,
/// since it would hang with no one to click through it.
/// </summary>
[SupportedOSPlatform("windows")]
public static class AppUninstallHelper
{
    private const string UninstallSubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string UninstallSubKeyWow = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    private static readonly Regex MsiProductCode = new(
        @"\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\}",
        RegexOptions.Compiled);

    /// <summary>One app's uninstall-relevant registry values.</summary>
    public sealed record UninstallEntry(
        string DisplayName, string? DisplayVersion, string KeyName,
        string? UninstallString, string? QuietUninstallString);

    /// <summary>The concrete process to run for a silent uninstall.</summary>
    public sealed record UninstallPlan(string FileName, string Arguments);

    public static async Task<CommandExecutionResult> UninstallAsync(
        string appName, string? version, bool isStoreApp, TimeSpan timeout,
        ILogger logger, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appName))
        {
            return CommandExecutionResult.Fail("No application name was provided.");
        }

        if (isStoreApp)
        {
            return await UninstallStoreAppAsync(appName, timeout, logger, cancellationToken);
        }

        var entry = FindDesktopEntry(appName, version, logger);
        if (entry is null)
        {
            return CommandExecutionResult.Fail(
                $"'{appName}' was not found in the uninstall registry (already removed?).");
        }

        var (plan, failureReason) = PlanUninstall(entry);
        if (plan is null)
        {
            return CommandExecutionResult.Fail(failureReason ?? "No silent uninstall method is available.");
        }

        logger.LogInformation("Uninstalling '{App}' via: {File} {Args}", appName, plan.FileName, plan.Arguments);
        var result = await ProcessRunner.RunAsync(plan.FileName, plan.Arguments, timeout, cancellationToken);

        if (result.TimedOut)
        {
            return CommandExecutionResult.Fail(
                $"Uninstall of '{appName}' timed out (it may have shown a prompt).", result.ExitCode);
        }

        // 0 = success; 3010 = success, reboot required (common for MSI).
        if (result.ExitCode is 0 or 3010)
        {
            return CommandExecutionResult.Ok(
                $"Uninstalled '{appName}'." + (result.ExitCode == 3010 ? " A reboot is required." : ""),
                result.ExitCode);
        }

        return CommandExecutionResult.Fail(
            $"Uninstaller for '{appName}' exited with code {result.ExitCode}.", result.ExitCode);
    }

    /// <summary>
    /// Decides how to silently uninstall a desktop app from its registry entry.
    /// Pure and side-effect-free so it can be unit-tested. Returns a plan, or a
    /// failure reason when no unattended method exists.
    /// </summary>
    public static (UninstallPlan? plan, string? failureReason) PlanUninstall(UninstallEntry entry)
    {
        // Prefer an MSI product code: msiexec is reliably silent with /qn.
        var productCode = ExtractMsiProductCode(entry.KeyName)
            ?? ExtractMsiProductCode(entry.UninstallString);

        if (productCode is not null)
        {
            return (new UninstallPlan("msiexec.exe", $"/x {productCode} /qn /norestart"), null);
        }

        // A QuietUninstallString is already unattended - use it verbatim.
        if (!string.IsNullOrWhiteSpace(entry.QuietUninstallString))
        {
            var (file, args) = SplitCommandLine(entry.QuietUninstallString);
            if (!string.IsNullOrWhiteSpace(file))
            {
                return (new UninstallPlan(file, args), null);
            }
        }

        // No MSI code and no QuietUninstallString (e.g. WinRAR, most NSIS/Inno
        // apps). Run the interactive uninstaller WITH a best-effort silent
        // switch - never bare, which would pop a dialog and hang in Session 0.
        // The per-command timeout still guards a switch the uninstaller ignores.
        if (!string.IsNullOrWhiteSpace(entry.UninstallString))
        {
            var (file, existingArgs) = SplitCommandLine(entry.UninstallString);
            if (!string.IsNullOrWhiteSpace(file))
            {
                var silent = GuessSilentSwitch(file);
                var args = string.IsNullOrWhiteSpace(existingArgs) ? silent : $"{existingArgs} {silent}";
                return (new UninstallPlan(file, args), null);
            }
        }

        return (null, $"'{entry.DisplayName}' has no uninstall information in the registry.");
    }

    /// <summary>
    /// Best-effort silent switch for an EXE uninstaller. Inno Setup uninstallers
    /// (unins###.exe) take /VERYSILENT; NSIS and most others (including WinRAR)
    /// take /S. This is a heuristic - the command timeout backs it up.
    /// </summary>
    public static string GuessSilentSwitch(string uninstallerPath)
    {
        // Inno Setup names its uninstaller "unins000.exe" (unins + digits) -
        // not to be confused with a generic "uninstall.exe" (WinRAR, NSIS).
        var name = Path.GetFileName(uninstallerPath);
        if (Regex.IsMatch(name, @"^unins\d", RegexOptions.IgnoreCase))
        {
            return "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART";
        }

        return "/S";
    }

    /// <summary>Pulls a {GUID} product code out of a key name or msiexec command.</summary>
    public static string? ExtractMsiProductCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = MsiProductCode.Match(value);
        return match.Success ? match.Value.ToUpperInvariant() : null;
    }

    /// <summary>Splits a command line into its executable and the remaining arguments.</summary>
    public static (string fileName, string arguments) SplitCommandLine(string commandLine)
    {
        var value = commandLine.Trim();
        if (value.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        if (value[0] == '"')
        {
            var end = value.IndexOf('"', 1);
            if (end > 0)
            {
                var file = value.Substring(1, end - 1);
                var args = value[(end + 1)..].Trim();
                return (file, args);
            }
            return (value.Trim('"'), string.Empty);
        }

        // Unquoted. A registry UninstallString is often a bare path that itself
        // contains spaces (e.g. "C:\Program Files\WinRAR\uninstall.exe"), so a
        // naive first-space split is wrong. Split at the end of the executable
        // instead: the first ".exe" followed by a space or end-of-string.
        var exe = FindExecutableEnd(value);
        if (exe > 0)
        {
            return (value[..exe], value[exe..].Trim());
        }

        var space = value.IndexOf(' ');
        return space < 0
            ? (value, string.Empty)
            : (value[..space], value[(space + 1)..].Trim());
    }

    /// <summary>Index just past the first ".exe" token boundary, or -1.</summary>
    private static int FindExecutableEnd(string value)
    {
        var search = 0;
        while (true)
        {
            var idx = value.IndexOf(".exe", search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return -1;
            }

            var end = idx + 4;
            if (end == value.Length || value[end] == ' ')
            {
                return end;
            }

            search = end;
        }
    }

    private static UninstallEntry? FindDesktopEntry(string appName, string? version, ILogger logger)
    {
        // Machine-wide (both registry views) then per-user hives, mirroring the
        // enumeration InstalledAppsHelper uses for the inventory scan.
        var roots = new List<(RegistryKey root, string path)>
        {
            (Registry.LocalMachine, UninstallSubKey),
            (Registry.LocalMachine, UninstallSubKeyWow)
        };

        try
        {
            foreach (var sid in Registry.Users.GetSubKeyNames())
            {
                if (!sid.StartsWith("S-1-5-21-", StringComparison.Ordinal) ||
                    sid.EndsWith("_Classes", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var userHive = Registry.Users.OpenSubKey(sid);
                if (userHive is not null)
                {
                    roots.Add((userHive, UninstallSubKey));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not enumerate per-user hives for uninstall lookup.");
        }

        UninstallEntry? versionlessMatch = null;

        foreach (var (root, path) in roots)
        {
            try
            {
                using var uninstallKey = root.OpenSubKey(path);
                if (uninstallKey is null)
                {
                    continue;
                }

                foreach (var keyName in uninstallKey.GetSubKeyNames())
                {
                    using var entryKey = uninstallKey.OpenSubKey(keyName);
                    var displayName = entryKey?.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(displayName) ||
                        !string.Equals(displayName.Trim(), appName.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var entry = new UninstallEntry(
                        displayName.Trim(),
                        (entryKey!.GetValue("DisplayVersion") as string)?.Trim(),
                        keyName,
                        entryKey.GetValue("UninstallString") as string,
                        entryKey.GetValue("QuietUninstallString") as string);

                    // Prefer an exact name+version match; fall back to name-only.
                    if (!string.IsNullOrWhiteSpace(version) &&
                        string.Equals(entry.DisplayVersion, version, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry;
                    }

                    versionlessMatch ??= entry;
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not read uninstall key {Path}.", path);
            }
        }

        return versionlessMatch;
    }

    private static async Task<CommandExecutionResult> UninstallStoreAppAsync(
        string appName, TimeSpan timeout, ILogger logger, CancellationToken cancellationToken)
    {
        // Store/UWP apps have no registry uninstall string; remove the package
        // by matching the reported name against installed packages (best-effort).
        var escaped = appName.Replace("'", "''");
        var script =
            "$ErrorActionPreference='Stop';" +
            $"$t='{escaped}';" +
            "$pkgs=Get-AppxPackage -AllUsers | Where-Object { $_.Name -like \"*$t*\" -or $_.PackageFullName -like \"*$t*\" };" +
            "if(-not $pkgs){ Write-Output 'NOTFOUND'; exit 2 }" +
            "foreach($p in $pkgs){ Remove-AppxPackage -Package $p.PackageFullName -AllUsers };" +
            "Write-Output 'REMOVED'";

        var result = await ProcessRunner.RunAsync(
            "powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
            timeout, cancellationToken);

        if (result.TimedOut)
        {
            return CommandExecutionResult.Fail($"Removal of Store app '{appName}' timed out.", result.ExitCode);
        }

        if (result.Output.Contains("NOTFOUND", StringComparison.OrdinalIgnoreCase))
        {
            return CommandExecutionResult.Fail($"No installed Store package matched '{appName}'.", result.ExitCode);
        }

        if (result.ExitCode == 0)
        {
            return CommandExecutionResult.Ok($"Removed Store app '{appName}'.", 0);
        }

        logger.LogWarning("Store app removal output: {Output}", result.Output);
        return CommandExecutionResult.Fail(
            $"Removing Store app '{appName}' failed (exit {result.ExitCode}).", result.ExitCode);
    }
}
