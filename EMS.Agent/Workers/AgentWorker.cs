using EMS.Agent.Configuration;
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
}
