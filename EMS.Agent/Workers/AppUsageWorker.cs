using System.Runtime.Versioning;
using EMS.Agent.Configuration;
using EMS.Agent.Services;
using Microsoft.Extensions.Options;

namespace EMS.Agent.Workers;

/// <summary>
/// Samples the foreground application on a short interval and periodically
/// uploads the accumulated per-app usage. Sampling and upload cadence are
/// independent: a fast sample tick (default 20s) keeps attribution accurate,
/// while a slower upload tick (default 10 min) keeps request volume low.
/// </summary>
[SupportedOSPlatform("windows")]
public class AppUsageWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAppUsageTrackerService _tracker;
    private readonly IWorkTimeTracker _workTimeTracker;
    private readonly ISessionStateService _sessionState;
    private readonly ApiSettings _apiSettings;
    private readonly ILogger<AppUsageWorker> _logger;

    public AppUsageWorker(
        IServiceScopeFactory scopeFactory,
        IAppUsageTrackerService tracker,
        IWorkTimeTracker workTimeTracker,
        ISessionStateService sessionState,
        IOptions<ApiSettings> apiSettings,
        ILogger<AppUsageWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _tracker = tracker;
        _workTimeTracker = workTimeTracker;
        _sessionState = sessionState;
        _apiSettings = apiSettings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sampleInterval = TimeSpan.FromSeconds(Math.Max(5, _apiSettings.AppUsageSampleIntervalSeconds));
        var uploadInterval = TimeSpan.FromMinutes(Math.Max(1, _apiSettings.AppUsageUploadIntervalMinutes));

        _logger.LogInformation(
            "App usage worker started. Sampling every {SampleSeconds}s, uploading every {UploadMinutes} min.",
            sampleInterval.TotalSeconds, uploadInterval.TotalMinutes);

        var elapsedSinceUpload = TimeSpan.Zero;

        // Fire the sleep beacon when the machine suspends (best-effort).
        _sessionState.Suspending += OnSuspending;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(sampleInterval, stoppingToken);

                Sample(sampleInterval);
                elapsedSinceUpload += sampleInterval;

                if (elapsedSinceUpload >= uploadInterval)
                {
                    await UploadUsageAsync(stoppingToken);
                    elapsedSinceUpload = TimeSpan.Zero;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Flush whatever was accumulated so a restart loses at most the
            // time since the last successful upload, not since the last hour.
            await UploadUsageAsync(CancellationToken.None);
        }
        finally
        {
            _sessionState.Suspending -= OnSuspending;
            _logger.LogInformation("App usage worker stopped.");
        }
    }

    private void OnSuspending()
    {
        // Runs on the SystemEvents thread; kick off a quick best-effort beacon
        // without blocking the suspend.
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var apiClient = scope.ServiceProvider.GetRequiredService<IApiClientService>();
                await apiClient.SendPowerStateAsync(suspended: true);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Suspend beacon failed.");
            }
        });
    }

    private void Sample(TimeSpan tickDuration)
    {
        try
        {
            _tracker.Sample(tickDuration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "App usage sampling failed for this tick.");
        }

        try
        {
            _workTimeTracker.Sample(tickDuration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Work-time sampling failed for this tick.");
        }
    }

    private async Task UploadUsageAsync(CancellationToken cancellationToken)
    {
        var usage = _tracker.FlushUsage();
        var workTime = _workTimeTracker.FlushDeltas();

        if (usage.Count == 0 && workTime.Count == 0)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var apiClient = scope.ServiceProvider.GetRequiredService<IApiClientService>();

            if (usage.Count > 0)
            {
                if (await apiClient.SendAppUsageAsync(usage, cancellationToken))
                {
                    _logger.LogInformation("Uploaded usage for {Count} application(s).", usage.Count);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to upload app usage for {Count} application(s); this period's data is not retried.",
                        usage.Count);
                }
            }

            if (workTime.Count > 0)
            {
                if (await apiClient.SendWorkTimeAsync(workTime, cancellationToken))
                {
                    _logger.LogInformation("Uploaded working time for {Count} day(s).", workTime.Count);
                }
                else
                {
                    _logger.LogWarning("Failed to upload working time; this period's data is not retried.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Usage/work-time upload failed.");
        }
    }
}
