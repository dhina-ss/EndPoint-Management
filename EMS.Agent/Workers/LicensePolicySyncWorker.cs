using EMS.Agent.Services;

namespace EMS.Agent.Workers;

/// <summary>
/// Before the device is activated, validates the bundled license key so the
/// agent learns its tenant's "require sign-in on startup" policy and can
/// enforce it (internet block) from the first boot. Once activated, the
/// heartbeat owns the policy, so this stops acting.
/// </summary>
public class LicensePolicySyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IActivationStore _activationStore;
    private readonly ILogger<LicensePolicySyncWorker> _logger;

    public LicensePolicySyncWorker(
        IServiceScopeFactory scopeFactory, IActivationStore activationStore,
        ILogger<LicensePolicySyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _activationStore = activationStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await SyncAsync(stoppingToken);
                // Re-check periodically in case the server was unreachable at boot.
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SyncAsync(CancellationToken cancellationToken)
    {
        // Once enrolled, the heartbeat delivers the per-device policy.
        if (_activationStore.IsActivated())
        {
            return;
        }

        var key = LicenseKeyFile.Read();
        if (string.IsNullOrWhiteSpace(key))
        {
            return; // No bundled license -> nothing to enforce pre-activation.
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var license = scope.ServiceProvider.GetRequiredService<ILicenseActivationService>();
            // Side effect: stores the tenant's require-login policy locally.
            await license.ValidateAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Pre-activation license policy sync failed (will retry).");
        }
    }
}
