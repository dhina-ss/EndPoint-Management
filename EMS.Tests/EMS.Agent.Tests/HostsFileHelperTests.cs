using System.Runtime.Versioning;
using EMS.Agent.Helpers;

namespace EMS.Agent.Tests;

[SupportedOSPlatform("windows")]
public class HostsFileHelperTests
{
    private const string BaseHosts =
        "# Copyright Microsoft Corp.\n127.0.0.1 localhost\n";

    [Fact]
    public void BuildUpdatedContent_AddsBlockWithWwwVariant()
    {
        var result = HostsFileHelper.BuildUpdatedContent(BaseHosts, new[] { "badsite.com" });

        Assert.Contains("127.0.0.1 localhost", result);              // preserves existing entries
        Assert.Contains("0.0.0.0 badsite.com", result);
        Assert.Contains("0.0.0.0 www.badsite.com", result);          // adds www. variant
        Assert.Contains("BEGIN EMS BLOCKLIST", result);
        Assert.Contains("END EMS BLOCKLIST", result);
    }

    [Fact]
    public void BuildUpdatedContent_ReplacesPreviousBlock_NoAccumulation()
    {
        var first = HostsFileHelper.BuildUpdatedContent(BaseHosts, new[] { "old.com" });
        var second = HostsFileHelper.BuildUpdatedContent(first, new[] { "new.com" });

        Assert.DoesNotContain("old.com", second);                    // old block fully removed
        Assert.Contains("0.0.0.0 new.com", second);
        // Exactly one managed block, never nested/duplicated.
        Assert.Equal(1, CountOccurrences(second, "BEGIN EMS BLOCKLIST"));
    }

    [Fact]
    public void BuildUpdatedContent_EmptyDomains_RemovesBlockButKeepsRest()
    {
        var blocked = HostsFileHelper.BuildUpdatedContent(BaseHosts, new[] { "badsite.com" });
        var cleared = HostsFileHelper.BuildUpdatedContent(blocked, Array.Empty<string>());

        Assert.DoesNotContain("BEGIN EMS BLOCKLIST", cleared);
        Assert.DoesNotContain("badsite.com", cleared);
        Assert.Contains("127.0.0.1 localhost", cleared);             // untouched entries survive
    }

    [Fact]
    public void BuildUpdatedContent_AlreadyWwwDomain_NotDoublePrefixed()
    {
        var result = HostsFileHelper.BuildUpdatedContent(BaseHosts, new[] { "www.badsite.com" });

        Assert.Contains("0.0.0.0 www.badsite.com", result);
        Assert.DoesNotContain("www.www.badsite.com", result);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
