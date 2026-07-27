using System.Runtime.Versioning;
using EMS.Agent.Helpers;

namespace EMS.Agent.Tests;

[SupportedOSPlatform("windows")]
public class AppUninstallHelperTests
{
    [Fact]
    public void ExtractMsiProductCode_FromBracedKeyName_ReturnsUppercaseGuid()
    {
        var code = AppUninstallHelper.ExtractMsiProductCode("{a1b2c3d4-1111-2222-3333-444455556666}");

        Assert.Equal("{A1B2C3D4-1111-2222-3333-444455556666}", code);
    }

    [Fact]
    public void ExtractMsiProductCode_FromMsiexecUninstallString_ReturnsGuid()
    {
        var code = AppUninstallHelper.ExtractMsiProductCode(
            "MsiExec.exe /X{A1B2C3D4-1111-2222-3333-444455556666}");

        Assert.Equal("{A1B2C3D4-1111-2222-3333-444455556666}", code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("C:\\Program Files\\App\\uninstall.exe")]
    public void ExtractMsiProductCode_WithoutGuid_ReturnsNull(string? value)
    {
        Assert.Null(AppUninstallHelper.ExtractMsiProductCode(value));
    }

    [Fact]
    public void PlanUninstall_MsiEntry_UsesMsiexecSilent()
    {
        var entry = new AppUninstallHelper.UninstallEntry(
            "7-Zip", "24.08", "{23170F69-40C1-2702-2408-000001000000}",
            "MsiExec.exe /I{23170F69-40C1-2702-2408-000001000000}", null);

        var (plan, reason) = AppUninstallHelper.PlanUninstall(entry);

        Assert.Null(reason);
        Assert.NotNull(plan);
        Assert.Equal("msiexec.exe", plan!.FileName);
        Assert.Equal("/x {23170F69-40C1-2702-2408-000001000000} /qn /norestart", plan.Arguments);
    }

    [Fact]
    public void PlanUninstall_QuietUninstallString_ParsesFileAndArgs()
    {
        var entry = new AppUninstallHelper.UninstallEntry(
            "Foo", "1.0", "Foo",
            "\"C:\\Program Files\\Foo\\uninstall.exe\"",
            "\"C:\\Program Files\\Foo\\uninstall.exe\" /S");

        var (plan, reason) = AppUninstallHelper.PlanUninstall(entry);

        Assert.Null(reason);
        Assert.NotNull(plan);
        Assert.Equal("C:\\Program Files\\Foo\\uninstall.exe", plan!.FileName);
        Assert.Equal("/S", plan.Arguments);
    }

    [Fact]
    public void PlanUninstall_OnlyInteractiveUninstallString_FailsRatherThanHang()
    {
        // No MSI code and no QuietUninstallString: running the bare interactive
        // uninstaller would hang in Session 0, so we refuse it.
        var entry = new AppUninstallHelper.UninstallEntry(
            "Legacy App", "2.0", "LegacyApp",
            "\"C:\\Program Files\\Legacy\\uninst.exe\"", null);

        var (plan, reason) = AppUninstallHelper.PlanUninstall(entry);

        Assert.Null(plan);
        Assert.NotNull(reason);
        Assert.Contains("no silent uninstall", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("\"C:\\a b\\x.exe\" /S", "C:\\a b\\x.exe", "/S")]
    [InlineData("C:\\a\\x.exe /quiet", "C:\\a\\x.exe", "/quiet")]
    [InlineData("C:\\a\\x.exe", "C:\\a\\x.exe", "")]
    public void SplitCommandLine_SeparatesExecutableFromArguments(string input, string file, string args)
    {
        var (fileName, arguments) = AppUninstallHelper.SplitCommandLine(input);

        Assert.Equal(file, fileName);
        Assert.Equal(args, arguments);
    }
}
