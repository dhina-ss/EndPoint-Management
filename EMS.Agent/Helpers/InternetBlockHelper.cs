using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace EMS.Agent.Helpers;

/// <summary>
/// Blocks / restores all internet access via the Windows Firewall, used to
/// enforce "require sign-in on every startup": while the device is enrolled,
/// the policy is on, and no one has signed in this power-on session, all
/// outbound traffic is blocked except the agent itself (so it can still reach
/// the server to verify the sign-in) and DNS. Requires SYSTEM/admin, so only
/// the service calls it.
/// </summary>
[SupportedOSPlatform("windows")]
public static class InternetBlockHelper
{
    private const string AgentRuleName = "TheLanceEndpoint-Allow-Agent";
    private const string DnsRuleName = "TheLanceEndpoint-Allow-DNS";

    /// <summary>Blocks all outbound traffic except the agent and DNS.</summary>
    public static void Block(ILogger logger)
    {
        var exe = Environment.ProcessPath ?? string.Empty;

        // Default all profiles to block outbound; allow rules below are the
        // only exceptions (loopback traffic bypasses the firewall entirely, so
        // a localhost server keeps working).
        Netsh("advfirewall set allprofiles firewallpolicy blockinbound,blockoutbound", logger);

        Netsh($"advfirewall firewall delete rule name=\"{AgentRuleName}\"", logger, ignoreError: true);
        if (!string.IsNullOrEmpty(exe))
        {
            Netsh($"advfirewall firewall add rule name=\"{AgentRuleName}\" dir=out action=allow " +
                  $"program=\"{exe}\" enable=yes profile=any", logger);
        }

        Netsh($"advfirewall firewall delete rule name=\"{DnsRuleName}\"", logger, ignoreError: true);
        Netsh($"advfirewall firewall add rule name=\"{DnsRuleName}\" dir=out action=allow " +
              "protocol=UDP remoteport=53 enable=yes profile=any", logger);

        logger.LogInformation("Internet access blocked pending sign-in.");
    }

    /// <summary>Restores normal outbound access (the Windows default).</summary>
    public static void Unblock(ILogger logger)
    {
        Netsh("advfirewall set allprofiles firewallpolicy blockinbound,allowoutbound", logger);
        Netsh($"advfirewall firewall delete rule name=\"{AgentRuleName}\"", logger, ignoreError: true);
        Netsh($"advfirewall firewall delete rule name=\"{DnsRuleName}\"", logger, ignoreError: true);
        logger.LogInformation("Internet access restored.");
    }

    private static void Netsh(string arguments, ILogger logger, bool ignoreError = false)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("netsh", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (process is null)
            {
                return;
            }

            process.WaitForExit(15000);
            if (process.ExitCode != 0 && !ignoreError)
            {
                logger.LogWarning("netsh {Args} exited with {Code}.", arguments, process.ExitCode);
            }
        }
        catch (Exception ex)
        {
            if (!ignoreError)
            {
                logger.LogWarning(ex, "netsh {Args} failed.", arguments);
            }
        }
    }
}
