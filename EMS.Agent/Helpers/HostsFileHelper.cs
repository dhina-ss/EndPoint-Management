using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;

namespace EMS.Agent.Helpers;

/// <summary>
/// Enforces website blocking by managing a marked section of the Windows
/// hosts file. The hosts file is consulted before DNS on every network, so a
/// blocked domain resolves to 0.0.0.0 (a dead address) regardless of which
/// Wi-Fi/VPN/LAN the device is on - satisfying "block from any network".
///
/// Only the region between the BEGIN/END markers is touched; anything else
/// in the file (including manual entries) is preserved. The block is
/// rewritten only when its content actually changes, so the DNS cache is
/// flushed at most once per policy change rather than every heartbeat.
/// </summary>
[SupportedOSPlatform("windows")]
public static class HostsFileHelper
{
    private const string BeginMarker = "# BEGIN EMS BLOCKLIST - managed by EMS Agent, do not edit";
    private const string EndMarker = "# END EMS BLOCKLIST";
    private const string BlackholeAddress = "0.0.0.0";

    private static string HostsFilePath =>
        Path.Combine(Environment.SystemDirectory, "drivers", "etc", "hosts");

    /// <summary>
    /// Ensures the hosts file blocks exactly the given domains (plus their
    /// www. variants). Passing an empty set removes the EMS block entirely.
    /// </summary>
    public static void ApplyBlocklist(IEnumerable<string> domains, ILogger logger)
    {
        try
        {
            var path = HostsFilePath;
            if (!File.Exists(path))
            {
                logger.LogWarning("Hosts file not found at {Path}; cannot apply website blocking.", path);
                return;
            }

            var existing = File.ReadAllText(path);
            var updated = BuildUpdatedContent(existing, domains);

            if (NormalizeForCompare(updated) == NormalizeForCompare(existing))
            {
                return;
            }

            File.WriteAllText(path, updated);
            FlushDnsCache(logger);
            logger.LogInformation("Website blocklist applied to the hosts file.");
        }
        catch (UnauthorizedAccessException ex)
        {
            // Expected when the tracker (non-elevated) runs this instead of
            // the service; the service run is the one that has rights.
            logger.LogWarning(ex, "No permission to write the hosts file; website blocking needs the service (SYSTEM) context.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to apply the website blocklist.");
        }
    }

    /// <summary>
    /// Pure transformation (no IO): removes any prior EMS block from
    /// <paramref name="existing"/> and appends a fresh block for the given
    /// domains. Exposed internally so the marker/round-trip logic can be
    /// unit tested without touching the real hosts file.
    /// </summary>
    internal static string BuildUpdatedContent(string existing, IEnumerable<string> domains)
    {
        var withoutBlock = RemoveExistingBlock(existing);
        var newBlock = BuildBlock(domains);

        return newBlock.Length == 0
            ? withoutBlock.TrimEnd('\r', '\n') + Environment.NewLine
            : withoutBlock.TrimEnd('\r', '\n') + Environment.NewLine + newBlock;
    }

    private static string RemoveExistingBlock(string content)
    {
        var beginIndex = content.IndexOf(BeginMarker, StringComparison.Ordinal);
        if (beginIndex < 0)
        {
            return content;
        }

        var endIndex = content.IndexOf(EndMarker, beginIndex, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            // Truncated marker block: drop from BEGIN onward.
            return content[..beginIndex];
        }

        var afterEnd = endIndex + EndMarker.Length;
        return content[..beginIndex] + content[afterEnd..];
    }

    private static string BuildBlock(IEnumerable<string> domains)
    {
        var hosts = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in domains)
        {
            var domain = raw?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(domain))
            {
                continue;
            }

            hosts.Add(domain);
            if (!domain.StartsWith("www.", StringComparison.Ordinal))
            {
                hosts.Add("www." + domain);
            }
        }

        if (hosts.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append(BeginMarker).Append(Environment.NewLine);
        foreach (var host in hosts)
        {
            builder.Append(BlackholeAddress).Append(' ').Append(host).Append(Environment.NewLine);
        }
        builder.Append(EndMarker).Append(Environment.NewLine);
        return builder.ToString();
    }

    // Line endings can differ between what we write and what other tools
    // normalize; compare on content, not exact bytes, to avoid needless writes.
    private static string NormalizeForCompare(string content)
        => content.Replace("\r\n", "\n").TrimEnd('\n');

    private static void FlushDnsCache(ILogger logger)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ipconfig",
                Arguments = "/flushdns",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            process?.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not flush the DNS cache; blocking still applies on the next lookup.");
        }
    }
}
