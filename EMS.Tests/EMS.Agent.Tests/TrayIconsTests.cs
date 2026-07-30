using System.Runtime.Versioning;
using EMS.Agent.Tray;

namespace EMS.Agent.Tests;

[SupportedOSPlatform("windows")]
public class TrayIconsTests
{
    [Fact]
    public void Activated_BuildsAValidIcon()
    {
        using var icon = TrayIcons.Activated();

        Assert.NotNull(icon);
        Assert.True(icon.Width > 0 && icon.Height > 0);
    }

    [Fact]
    public void NotActivated_BuildsAValidIcon()
    {
        using var icon = TrayIcons.NotActivated();

        Assert.NotNull(icon);
        Assert.True(icon.Width > 0 && icon.Height > 0);
    }
}
