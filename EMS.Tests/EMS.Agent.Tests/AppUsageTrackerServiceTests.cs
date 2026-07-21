using System.Runtime.Versioning;
using EMS.Agent.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace EMS.Agent.Tests;

[SupportedOSPlatform("windows")]
public class AppUsageTrackerServiceTests
{
    [Fact]
    public void FlushUsage_WithNoSamples_ReturnsEmpty()
    {
        var tracker = new AppUsageTrackerService(NullLogger<AppUsageTrackerService>.Instance);

        var usage = tracker.FlushUsage();

        Assert.Empty(usage);
    }

    [Fact]
    public void Sample_ThenFlush_AccumulatesPositiveDurationForSomeApp()
    {
        var tracker = new AppUsageTrackerService(NullLogger<AppUsageTrackerService>.Instance);

        // Whatever window has focus on this machine while the test runs;
        // deterministic across environments only in that *something* should
        // (an interactive session always has a foreground window).
        tracker.Sample(TimeSpan.FromSeconds(20));
        tracker.Sample(TimeSpan.FromSeconds(20));

        var usage = tracker.FlushUsage();

        Assert.All(usage, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.ApplicationName));
            Assert.True(entry.DurationSeconds > 0);
        });
    }

    [Fact]
    public void FlushUsage_ResetsAccumulatorAfterFlush()
    {
        var tracker = new AppUsageTrackerService(NullLogger<AppUsageTrackerService>.Instance);

        tracker.Sample(TimeSpan.FromSeconds(20));
        tracker.FlushUsage();

        var secondFlush = tracker.FlushUsage();

        Assert.Empty(secondFlush);
    }
}
