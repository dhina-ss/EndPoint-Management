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

    // When Defender status was last SUCCESSFULLY reported. Null forces a report
    // on the first cycle; a failed report leaves it null so the next cycle
    // retries. After the first success it runs on its own ~2-hour cadence.
    private DateTime? _lastSecurityReport;

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
            "The Lance Endpoint agent started. Server: {BaseUrl}, polling interval: {Interval} minutes.",
            _apiSettings.BaseUrl, _apiSettings.PollingIntervalMinutes);

        var pendingInterval = TimeSpan.FromSeconds(Math.Clamp(_apiSettings.PendingRegistrationIntervalSeconds, 5, 300));
        var normalInterval = TimeSpan.FromMinutes(Math.Max(1, _apiSettings.PollingIntervalMinutes));
        var registered = false;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (await RunCycleAsync(stoppingToken))
                {
                    registered = true;
                }
                // Until the device registers this run (dormant waiting for sign-in,
                // or just activated), poll quickly so it appears within seconds of
                // activation; afterwards use the normal inventory cadence.
                await Task.Delay(registered ? normalInterval : pendingInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown via the Service Control Manager or Ctrl+C.
        }
        finally
        {
            _logger.LogInformation("The Lance Endpoint agent stopped.");
        }
    }

    /// <summary>Runs one inventory cycle. Returns true when the device was
    /// successfully registered this cycle (used to switch off the fast startup
    /// polling); false when dormant, not sent, or on error.</summary>
    private async Task<bool> RunCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Scoped services per cycle; the singleton worker must not
            // capture the typed HttpClient for the process lifetime.
            using var scope = _scopeFactory.CreateScope();

            // Activation gate: until an EMS user signs in on this device, the
            // agent registers/collects/reports nothing. Under the require-login-
            // each-startup policy this also pauses until the user signs in for
            // the current power-on session.
            var gate = scope.ServiceProvider.GetRequiredService<IActivationGate>();
            if (!gate.IsMonitoringActive())
            {
                _logger.LogInformation("Waiting for sign-in; The Lance Endpoint is dormant until a user activates this session.");
                return false;
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

            // Microsoft Defender status + threat history (read-only). Runs on its
            // own ~2-hour cadence (first cycle immediately; retried each cycle
            // until the first success).
            if (ShouldReportSecurityStatus())
            {
                if (await ReportSecurityStatusAsync(apiClient, stoppingToken))
                {
                    _lastSecurityReport = DateTime.UtcNow;
                }
            }

            return sent;
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
            return false;
        }
    }

    /// <summary>
    /// True on the first cycle, then once the Defender-status interval has
    /// elapsed since the last SUCCESSFUL report (a failure leaves the timestamp
    /// null so the next cycle retries promptly).
    /// </summary>
    private bool ShouldReportSecurityStatus()
    {
        if (_lastSecurityReport is null)
        {
            return true;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _apiSettings.DefenderStatusIntervalMinutes));
        return DateTime.UtcNow - _lastSecurityReport.Value >= interval;
    }

    /// <summary>
    /// Reads Microsoft Defender status + threat history and reports it. Isolated
    /// so a failure never affects the rest of the cycle. Returns true only when
    /// the status was successfully reported.
    /// </summary>
    private async Task<bool> ReportSecurityStatusAsync(IApiClientService apiClient, CancellationToken stoppingToken)
    {
        try
        {
            var status = await DefenderStatusHelper.ReadAsync(
                _logger, TimeSpan.FromSeconds(90), stoppingToken);
            if (status is null)
            {
                // Could not read Defender (unavailable, third-party AV, or a
                // transient PowerShell error). Logged as a warning so the reason
                // is visible; the next cycle retries.
                _logger.LogWarning("Defender status was unavailable this cycle; will retry next cycle.");
                return false;
            }

            if (await apiClient.SendSecurityStatusAsync(status, stoppingToken))
            {
                _logger.LogInformation(
                    "Reported Defender status (realtime={Realtime}, av={Av}, {ThreatCount} threat(s)).",
                    status.RealtimeProtectionEnabled, status.AntivirusEnabled, status.Threats.Count);
                return true;
            }

            _logger.LogWarning("Defender status read but the server rejected the report; will retry next cycle.");
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Defender status report failed for this cycle.");
            return false;
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
