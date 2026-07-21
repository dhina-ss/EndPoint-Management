using System.Runtime.Versioning;
using EMS.Agent.Helpers;
using EMS.Agent.Models;

namespace EMS.Agent.Services;

/// <summary>
/// In-memory accumulator, one singleton instance shared by the sampling and
/// upload phases of <see cref="Workers.AppUsageWorker"/>. Usage since the
/// last successful upload is lost if the service restarts — acceptable for
/// v1, since it bounds data loss to at most one upload interval.
/// </summary>
[SupportedOSPlatform("windows")]
public class AppUsageTrackerService : IAppUsageTrackerService
{
    private readonly Dictionary<string, int> _secondsByApp = new();
    private readonly object _lock = new();
    private readonly ILogger<AppUsageTrackerService> _logger;

    public AppUsageTrackerService(ILogger<AppUsageTrackerService> logger)
    {
        _logger = logger;
    }

    public void Sample(TimeSpan tickDuration)
    {
        string? applicationName;
        try
        {
            applicationName = ForegroundWindowHelper.GetForegroundProcessName();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to sample the foreground application for this tick.");
            return;
        }

        if (string.IsNullOrWhiteSpace(applicationName))
        {
            return;
        }

        var seconds = (int)tickDuration.TotalSeconds;
        if (seconds <= 0)
        {
            return;
        }

        lock (_lock)
        {
            _secondsByApp.TryGetValue(applicationName, out var existing);
            _secondsByApp[applicationName] = existing + seconds;
        }
    }

    public IReadOnlyList<AppUsageModel> FlushUsage()
    {
        lock (_lock)
        {
            if (_secondsByApp.Count == 0)
            {
                return Array.Empty<AppUsageModel>();
            }

            var snapshot = _secondsByApp
                .Select(entry => new AppUsageModel
                {
                    ApplicationName = entry.Key,
                    DurationSeconds = entry.Value
                })
                .ToList();

            _secondsByApp.Clear();
            return snapshot;
        }
    }
}
