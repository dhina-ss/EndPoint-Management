using EMS.Agent.Configuration;
using EMS.Agent.Services;
using Microsoft.Extensions.Options;

namespace EMS.Agent.Workers;

/// <summary>
/// Sends a lightweight liveness heartbeat on a short interval, independent of
/// the (heavier, less frequent) inventory cycle in <see cref="AgentWorker"/>.
/// On startup the stored token from device-auth.json lets heartbeats
/// authenticate immediately; on a first-ever run they wait for registration.
/// </summary>
public class HeartbeatWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ApiSettings _apiSettings;
    private readonly ILogger<HeartbeatWorker> _logger;

    public HeartbeatWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ApiSettings> apiSettings,
        ILogger<HeartbeatWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _apiSettings = apiSettings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Heartbeat worker started. Interval: {Interval} seconds.", _apiSettings.HeartbeatIntervalSeconds);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await SendHeartbeatAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(_apiSettings.HeartbeatIntervalSeconds), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            _logger.LogInformation("Heartbeat worker stopped.");
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();

            // No heartbeat or policy enforcement until the device is activated.
            var activation = scope.ServiceProvider.GetRequiredService<IActivationStore>();
            if (!activation.IsActivated())
            {
                return;
            }

            var heartbeatService = scope.ServiceProvider.GetRequiredService<IHeartbeatService>();
            await heartbeatService.SendHeartbeatAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Heartbeat cycle failed; next attempt in {Interval} seconds.",
                _apiSettings.HeartbeatIntervalSeconds);
        }
    }
}
