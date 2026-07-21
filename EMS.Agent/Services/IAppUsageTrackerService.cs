using EMS.Agent.Models;

namespace EMS.Agent.Services;

/// <summary>
/// Accumulates foreground-application time in memory between uploads.
/// </summary>
public interface IAppUsageTrackerService
{
    /// <summary>Samples the current foreground app and credits it with one tick's duration.</summary>
    void Sample(TimeSpan tickDuration);

    /// <summary>Returns the accumulated per-app usage since the last flush, then resets to zero.</summary>
    IReadOnlyList<AppUsageModel> FlushUsage();
}
