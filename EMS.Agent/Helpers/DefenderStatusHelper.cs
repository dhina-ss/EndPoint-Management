using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using EMS.Agent.Models;

namespace EMS.Agent.Helpers;

/// <summary>
/// Reads Microsoft Defender status and threat history via the Defender
/// PowerShell module (Get-MpComputerStatus / Get-MpThreat / Get-MpThreatDetection).
/// Read-only: it reports what Defender already knows and has done. Returns null
/// when Defender is unavailable (third-party AV, Server without it, or an error).
/// </summary>
[SupportedOSPlatform("windows")]
public static class DefenderStatusHelper
{
    // Gathers status + threats and emits a single compact JSON object.
    private const string Script = @"
$ErrorActionPreference='SilentlyContinue'
# Ensure the Defender module is available even when it is not auto-loaded in
# the SYSTEM service's restricted environment.
Import-Module Defender -ErrorAction SilentlyContinue
$s = Get-MpComputerStatus
if (-not $s) { Write-Output '{}'; exit }
$defs=@{}; Get-MpThreat | ForEach-Object { $defs[[string]$_.ThreatID]=$_ }
$threats=@()
foreach ($d in (Get-MpThreatDetection)) {
  $t=$defs[[string]$d.ThreatID]
  $threats += [pscustomobject]@{
    name       = if($t){$t.ThreatName}else{'Threat ' + $d.ThreatID}
    severity   = if($t){[int]$t.SeverityID}else{$null}
    detectedAt = if($d.InitialDetectionTime){$d.InitialDetectionTime.ToUniversalTime().ToString('o')}else{$null}
    remediated = if($t){-not [bool]$t.IsActive}else{$true}
    resource   = ($d.Resources -join '; ')
  }
}
[pscustomobject]@{
  realtimeProtectionEnabled = [bool]$s.RealTimeProtectionEnabled
  antivirusEnabled          = [bool]$s.AntivirusEnabled
  lastQuickScan             = if($s.QuickScanEndTime){$s.QuickScanEndTime.ToUniversalTime().ToString('o')}else{$null}
  lastFullScan              = if($s.FullScanEndTime){$s.FullScanEndTime.ToUniversalTime().ToString('o')}else{$null}
  signatureVersion          = [string]$s.AntivirusSignatureVersion
  signatureAgeDays          = [int]$s.AntivirusSignatureAge
  engineVersion             = [string]$s.AMEngineVersion
  threats                   = $threats
} | ConvertTo-Json -Depth 4 -Compress
";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<SecurityStatusModel?> ReadAsync(
        ILogger logger, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        try
        {
            // -EncodedCommand takes a base64 UTF-16LE script, avoiding all
            // shell quoting issues with the multi-line script.
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(Script));
            var result = await ProcessRunner.RunAsync(
                "powershell.exe",
                $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
                timeout, cancellationToken);

            if (result.TimedOut)
            {
                logger.LogWarning("Reading Microsoft Defender status timed out.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(result.Output))
            {
                logger.LogWarning("Microsoft Defender status query returned no output.");
                return null;
            }

            var json = result.Output.Trim();
            var start = json.IndexOf('{');
            if (start < 0)
            {
                logger.LogWarning("Microsoft Defender status output was not JSON: {Output}",
                    Truncate(json, 300));
                return null;
            }
            json = json[start..];

            var status = JsonSerializer.Deserialize<SecurityStatusModel>(json, JsonOptions);
            // An empty object (Defender absent) deserializes to all-null; treat as unavailable.
            if (status is null or { RealtimeProtectionEnabled: null, AntivirusEnabled: null })
            {
                logger.LogWarning("Microsoft Defender reported no status (may be absent or a third-party AV is active).");
                return null;
            }
            return status;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read Microsoft Defender status.");
            return null;
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
