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
    private readonly ApiSettings _apiSettings;
    private readonly ILogger<AppUsageWorker> _logger;

    public AppUsageWorker(
        IServiceScopeFactory scopeFactory,
        IAppUsageTrackerService tracker,
        IOptions<ApiSettings> apiSettings,
        ILogger<AppUsageWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _tracker = tracker;
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
            _logger.LogInformation("App usage worker stopped.");
        }
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
    }

    private async Task UploadUsageAsync(CancellationToken cancellationToken)
    {
        var usage = _tracker.FlushUsage();
        if (usage.Count == 0)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var apiClient = scope.ServiceProvider.GetRequiredService<IApiClientService>();
            var sent = await apiClient.SendAppUsageAsync(usage, cancellationToken);

            if (sent)
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "App usage upload failed.");
        }
    }
}
