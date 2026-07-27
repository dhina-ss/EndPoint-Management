using EMS.Agent.Configuration;
using EMS.Agent.Helpers;
using EMS.Agent.Services;
using Microsoft.Extensions.Options;

namespace EMS.Agent.Workers;

/// <summary>
/// Main agent loop: collects the device inventory and reports it to the EMS
/// server, then sleeps for the configured polling interval. A failing cycle —
/// collection error, network failure, API down — is logged and never stops
/// the service; the next cycle is the retry.
/// </summary>
public class AgentWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ApiSettings _apiSettings;
    private readonly ILogger<AgentWorker> _logger;

    // When the installed-app list was last reported. Null forces a scan on the
    // first cycle after startup; thereafter it runs on its own hourly cadence.
    private DateTime? _lastInstalledAppsReport;

    public AgentWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ApiSettings> apiSettings,
        ILogger<AgentWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _apiSettings = apiSettings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "EMS Agent started. Server: {BaseUrl}, polling interval: {Interval} minutes.",
            _apiSettings.BaseUrl, _apiSettings.PollingIntervalMinutes);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunCycleAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(_apiSettings.PollingIntervalMinutes), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown via the Service Control Manager or Ctrl+C.
        }
        finally
        {
            _logger.LogInformation("EMS Agent stopped.");
        }
    }

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Scoped services per cycle; the singleton worker must not
            // capture the typed HttpClient for the process lifetime.
            using var scope = _scopeFactory.CreateScope();

            // Activation gate: until an EMS user signs in on this device, the
            // agent registers/collects/reports nothing.
            var activation = scope.ServiceProvider.GetRequiredService<IActivationStore>();
            if (!activation.IsActivated())
            {
                _logger.LogInformation("Waiting for activation; sign in via the EMS Agent login to activate.");
                return;
            }

            var collector = scope.ServiceProvider.GetRequiredService<IDeviceCollectorService>();
            var apiClient = scope.ServiceProvider.GetRequiredService<IApiClientService>();

            var inventory = await collector.CollectAsync(stoppingToken);
            _logger.LogInformation(
                "Inventory collected for device {DeviceId} ({DeviceName}).",
                inventory.DeviceId, inventory.DeviceName);

            var sent = await apiClient.RegisterDeviceAsync(inventory, stoppingToken);

            if (sent)
            {
                _logger.LogInformation("Inventory sent to the EMS server successfully.");
            }
            else
            {
                _logger.LogWarning(
                    "Inventory could not be sent to the EMS server; next attempt in {Interval} minutes.",
                    _apiSettings.PollingIntervalMinutes);
            }

            // Installed software changes rarely, so the app scan runs on its
            // own slower cadence (default hourly), independent of this cycle.
            if (ShouldReportInstalledApps())
            {
                if (await ReportInstalledAppsAsync(apiClient, stoppingToken))
                {
                    _lastInstalledAppsReport = DateTime.UtcNow;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Agent cycle failed; the agent keeps running, next attempt in {Interval} minutes.",
                _apiSettings.PollingIntervalMinutes);
        }
    }

    /// <summary>
    /// True on the first cycle, then once the configured installed-apps
    /// interval has elapsed since the last successful report.
    /// </summary>
    private bool ShouldReportInstalledApps()
    {
        if (_lastInstalledAppsReport is null)
        {
            return true;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _apiSettings.InstalledAppsIntervalMinutes));
        return DateTime.UtcNow - _lastInstalledAppsReport.Value >= interval;
    }

    /// <summary>
    /// Scans and uploads installed software. Isolated from the main cycle so
    /// a scan failure never prevents the hardware inventory from reporting.
    /// Returns true only when the list was successfully reported.
    /// </summary>
    private async Task<bool> ReportInstalledAppsAsync(IApiClientService apiClient, CancellationToken stoppingToken)
    {
        try
        {
            var applications = InstalledAppsHelper.Collect(_logger);
            if (applications.Count == 0)
            {
                return false;
            }

            if (await apiClient.SendInstalledAppsAsync(applications, stoppingToken))
            {
                _logger.LogInformation("Reported {Count} installed application(s).", applications.Count);
                return true;
            }

            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Installed-application scan failed for this cycle.");
            return false;
        }
    }
}
