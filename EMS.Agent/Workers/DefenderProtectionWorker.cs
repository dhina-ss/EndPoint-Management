using System.Runtime.Versioning;
using EMS.Agent.Helpers;
using EMS.Agent.Services;

namespace EMS.Agent.Workers;

/// <summary>
/// Keeps the device protected by Microsoft Defender: re-asserts real-time
/// protection (and cloud/PUA hardening) on a short interval, and runs a Defender
/// quick scan that removes viruses, ransomware, and spyware on a daily interval.
/// Runs only in the SYSTEM service (Defender management needs admin) and only on
/// an activated device. What Defender finds/removes is reported by the existing
/// security-status post, which drives the dashboard's protection status.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DefenderProtectionWorker : BackgroundService
{
    private static readonly TimeSpan EnsureInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ScanInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan EnsureTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ScanTimeout = TimeSpan.FromMinutes(40);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DefenderProtectionWorker> _logger;
    private DateTime _lastScanUtc = DateTime.MinValue;

    public DefenderProtectionWorker(IServiceScopeFactory scopeFactory, ILogger<DefenderProtectionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Defender protection worker started.");
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await CycleAsync(stoppingToken);
                await Task.Delay(EnsureInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task CycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var activation = scope.ServiceProvider.GetRequiredService<IActivationStore>();
            if (!activation.IsActivated())
            {
                return;
            }

            // Always keep real-time protection on (cheap).
            await DefenderProtectionHelper.EnsureProtectionAsync(_logger, EnsureTimeout, stoppingToken);

            // Run a removing quick scan once per day.
            if (DateTime.UtcNow - _lastScanUtc >= ScanInterval)
            {
                await DefenderProtectionHelper.RunQuickScanAndRemediateAsync(_logger, ScanTimeout, stoppingToken);
                _lastScanUtc = DateTime.UtcNow;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Defender protection cycle failed; will retry next interval.");
        }
    }
}
