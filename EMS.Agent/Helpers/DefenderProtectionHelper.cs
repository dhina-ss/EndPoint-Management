using System.Runtime.Versioning;
using System.Text;

namespace EMS.Agent.Helpers;

/// <summary>
/// Actively manages Microsoft Defender - the OS antivirus engine - so the
/// endpoint stays protected against viruses, ransomware, and spyware:
///   * EnsureProtection keeps real-time protection, behaviour monitoring,
///     cloud (MAPS) and PUA blocking turned on (re-enabling them if a user or
///     malware turned them off).
///   * RunQuickScanAndRemediate runs a Defender quick scan and removes any
///     active threats it finds (Defender also auto-remediates during the scan).
/// Both need SYSTEM/admin, so only the service calls them. Reporting of what
/// Defender found/removed is handled separately by DefenderStatusHelper, which
/// the AgentWorker already posts to the server.
/// </summary>
[SupportedOSPlatform("windows")]
public static class DefenderProtectionHelper
{
    private const string EnsureScript = @"
$ErrorActionPreference='SilentlyContinue'
Set-MpPreference -DisableRealtimeMonitoring $false
Set-MpPreference -DisableBehaviorMonitoring $false
Set-MpPreference -DisableIOAVProtection $false
Set-MpPreference -DisableScriptScanning $false
Set-MpPreference -MAPSReporting Advanced
Set-MpPreference -SubmitSamplesConsent SendSafeSamples
Set-MpPreference -PUAProtection Enabled
$s = Get-MpComputerStatus
if ($s) { Write-Output ([bool]$s.RealTimeProtectionEnabled) } else { Write-Output 'false' }
";

    private const string ScanScript = @"
$ErrorActionPreference='SilentlyContinue'
Start-MpScan -ScanType QuickScan
$active = @(Get-MpThreat | Where-Object { $_.IsActive })
if ($active.Count -gt 0) { Remove-MpThreat }
Write-Output $active.Count
";

    /// <summary>Turns real-time protection and hardening back on. Returns true if RTP is on afterward.</summary>
    public static async Task<bool> EnsureProtectionAsync(
        ILogger logger, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var output = await RunAsync(EnsureScript, timeout, cancellationToken);
        var on = output.Trim().StartsWith("true", StringComparison.OrdinalIgnoreCase);
        logger.LogInformation("Defender protection ensured. Real-time protection on: {On}.", on);
        return on;
    }

    /// <summary>Runs a quick scan and removes active threats. Returns how many active threats were removed.</summary>
    public static async Task<int> RunQuickScanAndRemediateAsync(
        ILogger logger, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var output = await RunAsync(ScanScript, timeout, cancellationToken);
        int.TryParse(output.Trim().Split('\n').LastOrDefault()?.Trim(), out var removed);
        logger.LogInformation("Defender quick scan complete. Active threats removed: {Removed}.", removed);
        return removed;
    }

    private static async Task<string> RunAsync(string script, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var result = await ProcessRunner.RunAsync(
                "powershell.exe",
                $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
                timeout, cancellationToken);
            return result.TimedOut ? string.Empty : (result.Output ?? string.Empty);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return string.Empty;
        }
    }
}
