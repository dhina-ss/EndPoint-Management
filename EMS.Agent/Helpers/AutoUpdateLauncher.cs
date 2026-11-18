using System.Diagnostics;
using System.Runtime.Versioning;

namespace EMS.Agent.Helpers;

/// <summary>
/// Launches the agent update installer in a detached background script so that
/// when the installer stops and restarts the EMSAgent Windows service, the installer
/// is not killed with it.
/// </summary>
[SupportedOSPlatform("windows")]
public static class AutoUpdateLauncher
{
    public static bool Launch(string installerPath, ILogger logger)
    {
        try
        {
            var updateDir = Path.GetDirectoryName(installerPath)
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EMS.Agent", "updates");

            if (!Directory.Exists(updateDir))
            {
                Directory.CreateDirectory(updateDir);
            }

            var scriptPath = Path.Combine(updateDir, "apply-update.cmd");

            // We give the running service 4 seconds to finish logging and release handles,
            // then invoke the Inno Setup installer silently. Once completed, clean up.
            var scriptContent = $@"@echo off
setlocal
timeout /t 4 /nobreak >nul
""{installerPath}"" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-
timeout /t 5 /nobreak >nul
if exist ""{installerPath}"" del /f /q ""{installerPath}""
(goto) 2>nul & del /f /q ""%~f0"" & exit
";

            File.WriteAllText(scriptPath, scriptContent);

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c start \"\" /min \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(psi);
            logger.LogInformation("Detached auto-update launcher started for installer {InstallerPath}.", installerPath);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to launch detached auto-update script for installer at {InstallerPath}.", installerPath);
            return false;
        }
    }
}
