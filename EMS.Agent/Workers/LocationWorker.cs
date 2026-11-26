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
    private readonly ISessionStateService? _sessionState;
    private readonly SemaphoreSlim _wakeSignal = new(0, 1);

    public LocationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ApiSettings> apiSettings,
        ILogger<LocationWorker> logger,
        ISessionStateService? sessionState = null)
    {
        _scopeFactory = scopeFactory;
        _apiSettings = apiSettings.Value;
        _logger = logger;
        _sessionState = sessionState;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(5, _apiSettings.LocationIntervalMinutes));
        var retryInterval = TimeSpan.FromMinutes(1);
        _logger.LogInformation("Location worker started. Interval: {Minutes} min.", interval.TotalMinutes);

        void OnResumed()
        {
            if (_wakeSignal.CurrentCount == 0)
            {
                try { _wakeSignal.Release(); } catch { }
            }
        }

        if (_sessionState is not null)
        {
            _sessionState.Resumed += OnResumed;
        }

        try
        {
            // Brief startup delay before first location check.
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

            var hasReported = false;
            while (!stoppingToken.IsCancellationRequested)
            {
                var sent = await ReportLocationAsync(stoppingToken);
                if (sent)
                {
                    hasReported = true;
                }

                // If we haven't delivered an initial fix yet (waiting on sign-in, token,
                // or Wi-Fi settle), retry every 1 minute instead of sleeping 1 hour.
                var waitTime = hasReported ? interval : retryInterval;
                var woke = await _wakeSignal.WaitAsync(waitTime, stoppingToken);
                if (woke)
                {
                    // Wait briefly after wake from sleep for Wi-Fi to reconnect.
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            if (_sessionState is not null)
            {
                _sessionState.Resumed -= OnResumed;
            }
            _wakeSignal.Dispose();
            _logger.LogInformation("Location worker stopped.");
        }
    }

    private async Task<bool> ReportLocationAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();

            // Only report once monitoring is active for this session (same gate
            // as the rest, including require-login-each-startup).
            var gate = scope.ServiceProvider.GetRequiredService<IActivationGate>();
            if (!gate.IsMonitoringActive())
            {
                return false;
            }

            var reading = await GpsLocationHelper.TryReadAsync(_logger, stoppingToken);
            if (reading is null)
            {
                return false; // No GPS; the server's IP location stands.
            }

            var apiClient = scope.ServiceProvider.GetRequiredService<IApiClientService>();
            return await apiClient.SendLocationAsync(
                reading.Latitude, reading.Longitude, reading.AccuracyMeters, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Location report cycle failed.");
            return false;
        }
    }
}
