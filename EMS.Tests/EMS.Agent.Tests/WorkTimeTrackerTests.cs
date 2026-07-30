using System.Runtime.Versioning;
using EMS.Agent.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace EMS.Agent.Tests;

[SupportedOSPlatform("windows")]
public class WorkTimeTrackerTests
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(20);

    private static (WorkTimeTracker tracker, FakeSession session, FakeClock clock) Create()
    {
        var session = new FakeSession();
        var clock = new FakeClock(new DateTimeOffset(2026, 7, 27, 9, 0, 0, TimeSpan.Zero));
        var tracker = new WorkTimeTracker(session, NullLogger<WorkTimeTracker>.Instance, clock);
        return (tracker, session, clock);
    }

    [Fact]
    public void Sample_UnlockedContiguousTicks_AccumulatesTime()
    {
        var (tracker, _, clock) = Create();

        tracker.Sample(Tick);                 // first tick (counts)
        clock.Advance(Tick);
        tracker.Sample(Tick);                 // contiguous tick (counts)

        var deltas = tracker.FlushDeltas();
        var day = Assert.Single(deltas);
        Assert.Equal(40, day.SecondsDelta);
    }

    [Fact]
    public void Sample_WhileLocked_AddsNothing()
    {
        var (tracker, session, clock) = Create();
        session.IsLocked = true;

        tracker.Sample(Tick);
        clock.Advance(Tick);
        tracker.Sample(Tick);

        Assert.Empty(tracker.FlushDeltas());
    }

    [Fact]
    public void Sample_AfterLargeGap_DoesNotCountTheSleepSpan()
    {
        var (tracker, _, clock) = Create();

        tracker.Sample(Tick);                 // counts (20s)
        clock.Advance(TimeSpan.FromHours(2)); // machine slept
        tracker.Sample(Tick);                 // gap >> tick -> not counted

        var day = Assert.Single(tracker.FlushDeltas());
        Assert.Equal(20, day.SecondsDelta);
    }

    [Fact]
    public void FlushDeltas_ClearsAccumulator()
    {
        var (tracker, _, _) = Create();
        tracker.Sample(Tick);

        Assert.NotEmpty(tracker.FlushDeltas());
        Assert.Empty(tracker.FlushDeltas());
    }

    [Fact]
    public void Sample_BucketsByLocalDay()
    {
        var (tracker, _, clock) = Create();

        tracker.Sample(Tick);                     // day 1
        clock.Set(new DateTimeOffset(2026, 7, 28, 9, 0, 0, TimeSpan.Zero));
        tracker.Sample(Tick);                     // day 2 (new day; gap large -> not counted, but bucket exists?)

        // The day-2 sample has a huge gap so it isn't counted; only day 1 has time.
        var deltas = tracker.FlushDeltas();
        var day = Assert.Single(deltas);
        Assert.Equal(new DateOnly(2026, 7, 27), day.WorkDate);
    }

    private sealed class FakeSession : ISessionStateService
    {
        public bool IsLocked { get; set; }
        public event Action? Suspending { add { } remove { } }
    }

    private sealed class FakeClock : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeClock(DateTimeOffset start) => _now = start;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
        public void Set(DateTimeOffset to) => _now = to;
        public override DateTimeOffset GetUtcNow() => _now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
