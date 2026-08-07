using System.Runtime.Versioning;
using EMS.Agent.Configuration;
using EMS.Agent.Helpers;
using EMS.Agent.Services;
using Microsoft.Extensions.Options;

namespace EMS.Agent.Workers;

/// <summary>
/// Reports a precise Windows GPS/Wi-Fi location fix on a slow cadence, from the
/// per-user tracker process (the Location API needs the interactive session).
/// Entirely best-effort: where Location Services are off/denied/absent the read
/// returns null and nothing is sent, so the server keeps its IP-based location.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public class LocationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ApiSettings _apiSettings;
    private readonly ILogger<LocationWorker> _logger;

    public LocationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ApiSettings> apiSettings,
        ILogger<LocationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _apiSettings = apiSettings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(5, _apiSettings.LocationIntervalMinutes));
        _logger.LogInformation("Location worker started. Interval: {Minutes} min.", interval.TotalMinutes);

        try
        {
            // Let the session settle before the first fix.
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await ReportLocationAsync(stoppingToken);
                await Task.Delay(interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            _logger.LogInformation("Location worker stopped.");
        }
    }

    private async Task ReportLocationAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();

            // Only report once the device is activated (same gate as the rest).
            var activation = scope.ServiceProvider.GetRequiredService<IActivationStore>();
            if (!activation.IsActivated())
            {
                return;
            }

            var reading = await GpsLocationHelper.TryReadAsync(_logger, stoppingToken);
            if (reading is null)
            {
                return; // No GPS; the server's IP location stands.
            }

            var apiClient = scope.ServiceProvider.GetRequiredService<IApiClientService>();
            await apiClient.SendLocationAsync(
                reading.Latitude, reading.Longitude, reading.AccuracyMeters, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Location report cycle failed.");
        }
    }
}
