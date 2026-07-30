using System.Runtime.Versioning;
using EMS.Agent.Models;

namespace EMS.Agent.Services;

/// <summary>
/// Singleton accumulator for working hours, shared by the sample and upload
/// phases of <see cref="Workers.AppUsageWorker"/> (per-user tracker process).
///
/// The timer starts at login (this process starts on logon) and stops at
/// logoff/shutdown (the process stops). A tick is counted only when the session
/// is unlocked AND the real elapsed wall-clock since the previous tick is close
/// to the tick interval - a large gap means the machine slept (the sample loop
/// was frozen), so that span is not counted. Time is bucketed by device-local
/// calendar day.
/// </summary>
[SupportedOSPlatform("windows")]
public class WorkTimeTracker : IWorkTimeTracker
{
    private readonly Dictionary<DateOnly, int> _secondsByDate = new();
    private readonly object _lock = new();
    private readonly ISessionStateService _session;
    private readonly TimeProvider _time;
    private readonly ILogger<WorkTimeTracker> _logger;

    // Real (wall-clock) time of the previous sample. UtcNow advances across
    // sleep, so a big jump here reveals a sleep/freeze gap to skip.
    private DateTime _lastSampleUtc;

    public WorkTimeTracker(
        ISessionStateService session, ILogger<WorkTimeTracker> logger, TimeProvider? timeProvider = null)
    {
        _session = session;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
    }

    public void Sample(TimeSpan tickDuration)
    {
        var seconds = (int)tickDuration.TotalSeconds;
        if (seconds <= 0)
        {
            return;
        }

        // Bucket by the device's LOCAL day - that is the user's workday.
        var localDate = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        var nowUtc = _time.GetUtcNow().UtcDateTime;

        lock (_lock)
        {
            var previous = _lastSampleUtc;
            _lastSampleUtc = nowUtc;

            // A gap much larger than one tick means the machine slept (or the
            // process was frozen) between samples - do not count that span.
            if (previous != default && nowUtc - previous > tickDuration + tickDuration)
            {
                return;
            }

            // Locked screen pauses the timer (per configuration/choice).
            if (_session.IsLocked)
            {
                return;
            }

            _secondsByDate.TryGetValue(localDate, out var existing);
            _secondsByDate[localDate] = existing + seconds;
        }
    }

    public IReadOnlyList<WorkTimeModel> FlushDeltas()
    {
        lock (_lock)
        {
            if (_secondsByDate.Count == 0)
            {
                return Array.Empty<WorkTimeModel>();
            }

            var snapshot = _secondsByDate
                .Select(entry => new WorkTimeModel { WorkDate = entry.Key, SecondsDelta = entry.Value })
                .ToList();

            _secondsByDate.Clear();
            return snapshot;
        }
    }
}
