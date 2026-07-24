using System.Runtime.Versioning;
using EMS.Agent.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace EMS.Agent.Tests;

/// <summary>
/// Runs the real registry/WMI scan against this machine. Assertions are
/// about shape and plausibility, not specific software, so they hold on any
/// Windows host.
/// </summary>
[SupportedOSPlatform("windows")]
public class InstalledAppsHelperTests
{
    [Fact]
    public void Collect_FindsInstalledApplications()
    {
        var apps = InstalledAppsHelper.Collect(NullLogger.Instance);

        // Any real Windows install has software registered.
        Assert.NotEmpty(apps);

        // Every entry must at least be named - that is the display key.
        Assert.All(apps, app => Assert.False(string.IsNullOrWhiteSpace(app.Name)));

        // Results are sorted for stable presentation.
        var names = apps.Select(a => a.Name).ToList();
        Assert.Equal(names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase), names);
    }

    [Fact]
    public void Collect_ResolvesExecutablesForSomeDesktopApps()
    {
        var apps = InstalledAppsHelper.Collect(NullLogger.Instance);

        // Executable names drive blocking; at least some desktop apps should
        // resolve one, and every resolved value must be a bare .exe file
        // name rather than a full path.
        var withExecutables = apps.Where(a => a.ExecutableName is not null).ToList();
        Assert.NotEmpty(withExecutables);

        Assert.All(withExecutables, app =>
        {
            Assert.EndsWith(".exe", app.ExecutableName!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain('\\', app.ExecutableName!);
            Assert.DoesNotContain('/', app.ExecutableName!);
        });
    }
}
