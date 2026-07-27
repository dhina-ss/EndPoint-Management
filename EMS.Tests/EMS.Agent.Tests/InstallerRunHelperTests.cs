using EMS.Agent.Helpers;

namespace EMS.Agent.Tests;

public class InstallerRunHelperTests
{
    [Fact]
    public void BuildInstallPlan_Msi_UsesQuietMsiexec()
    {
        var plan = InstallerRunHelper.BuildInstallPlan("Msi", @"C:\temp\pkg.msi", null);

        Assert.Equal("msiexec.exe", plan.FileName);
        Assert.Equal("/i \"C:\\temp\\pkg.msi\" /qn /norestart", plan.Arguments);
    }

    [Fact]
    public void BuildInstallPlan_MsiWithExtraArgs_AppendsThem()
    {
        var plan = InstallerRunHelper.BuildInstallPlan("Msi", @"C:\temp\pkg.msi", "ALLUSERS=1");

        Assert.Equal("/i \"C:\\temp\\pkg.msi\" /qn /norestart ALLUSERS=1", plan.Arguments);
    }

    [Fact]
    public void BuildInstallPlan_MsiInferredFromExtension_WhenKindMissing()
    {
        var plan = InstallerRunHelper.BuildInstallPlan(null, @"C:\temp\setup.msi", null);

        Assert.Equal("msiexec.exe", plan.FileName);
    }

    [Fact]
    public void BuildInstallPlan_Exe_RunsFileWithSilentArgs()
    {
        var plan = InstallerRunHelper.BuildInstallPlan("Exe", @"C:\temp\setup.exe", "/S");

        Assert.Equal(@"C:\temp\setup.exe", plan.FileName);
        Assert.Equal("/S", plan.Arguments);
    }

    [Fact]
    public void BuildInstallPlan_ExeWithoutArgs_HasEmptyArguments()
    {
        var plan = InstallerRunHelper.BuildInstallPlan("Exe", @"C:\temp\setup.exe", null);

        Assert.Equal(@"C:\temp\setup.exe", plan.FileName);
        Assert.Equal(string.Empty, plan.Arguments);
    }
}
