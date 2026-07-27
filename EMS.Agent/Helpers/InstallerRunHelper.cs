using EMS.Agent.Models;

namespace EMS.Agent.Helpers;

/// <summary>
/// Runs a downloaded installer package silently. MSI packages always use
/// <c>msiexec /i ... /qn /norestart</c>; EXE packages use the silent switches
/// the admin supplied when uploading the package (an EXE with no silent switch
/// would show UI and hang in Session 0, so the per-command timeout guards it).
/// </summary>
public static class InstallerRunHelper
{
    /// <summary>What to execute for a given package.</summary>
    public sealed record InstallPlan(string FileName, string Arguments);

    /// <summary>
    /// Builds the command line for an installer. Pure/testable. <paramref name="kind"/>
    /// is "Msi" or "Exe" (case-insensitive).
    /// </summary>
    public static InstallPlan BuildInstallPlan(string? kind, string filePath, string? silentArgs)
    {
        var isMsi = string.Equals(kind, "Msi", StringComparison.OrdinalIgnoreCase)
            || filePath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);

        if (isMsi)
        {
            var extra = string.IsNullOrWhiteSpace(silentArgs) ? string.Empty : " " + silentArgs.Trim();
            return new InstallPlan("msiexec.exe", $"/i \"{filePath}\" /qn /norestart{extra}");
        }

        return new InstallPlan(filePath, silentArgs?.Trim() ?? string.Empty);
    }

    public static async Task<CommandExecutionResult> RunAsync(
        string kind, string filePath, string? silentArgs, TimeSpan timeout,
        ILogger logger, CancellationToken cancellationToken = default)
    {
        var plan = BuildInstallPlan(kind, filePath, silentArgs);

        logger.LogInformation("Running installer: {File} {Args}", plan.FileName, plan.Arguments);
        var result = await ProcessRunner.RunAsync(plan.FileName, plan.Arguments, timeout, cancellationToken);

        if (result.TimedOut)
        {
            return CommandExecutionResult.Fail(
                "Installer timed out (an EXE may need different silent switches).", result.ExitCode);
        }

        // 0 = success; 3010 = success, reboot required; 1641 = success, reboot initiated.
        if (result.ExitCode is 0 or 3010 or 1641)
        {
            var note = result.ExitCode is 3010 or 1641 ? " A reboot is required." : "";
            return CommandExecutionResult.Ok($"Installer completed successfully.{note}", result.ExitCode);
        }

        return CommandExecutionResult.Fail($"Installer exited with code {result.ExitCode}.", result.ExitCode);
    }
}
