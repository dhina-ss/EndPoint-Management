using EMS.Agent.Models;

namespace EMS.Agent.Services;

/// <summary>Accumulates working time (logged-in, awake, unlocked) per local day.</summary>
public interface IWorkTimeTracker
{
    /// <summary>Records one sample tick's worth of working time, if it counts.</summary>
    void Sample(TimeSpan tickDuration);

    /// <summary>Returns per-day deltas since the last flush and clears them.</summary>
    IReadOnlyList<WorkTimeModel> FlushDeltas();
}
