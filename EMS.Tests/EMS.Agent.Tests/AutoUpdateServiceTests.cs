using EMS.Agent.Services;

namespace EMS.Agent.Tests;

public class AutoUpdateServiceTests
{
    [Theory]
    [InlineData("1.0.28", "1.0.27", true)]
    [InlineData("1.1.0", "1.0.27", true)]
    [InlineData("2.0.0", "1.9.99", true)]
    [InlineData("v1.0.28", "1.0.27", true)]
    [InlineData("1.0.28", "v1.0.27", true)]
    [InlineData("1.0.27", "1.0.27", false)]
    [InlineData("1.0.26", "1.0.27", false)]
    [InlineData("0.9.9", "1.0.0", false)]
    [InlineData(null, "1.0.0", false)]
    [InlineData("", "1.0.0", false)]
    [InlineData("   ", "1.0.0", false)]
    public void IsNewerVersion_EvaluatesCorrectly(string? latest, string current, bool expected)
    {
        var result = AutoUpdateService.IsNewerVersion(latest, current);
        Assert.Equal(expected, result);
    }
}
