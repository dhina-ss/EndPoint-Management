using System.Runtime.Versioning;
using EMS.Agent.Helpers;
using EMS.Agent.Services;

namespace EMS.Agent.Workers;

/// <summary>
/// Enforces the "require sign-in on every startup" policy by cutting internet
/// access until the user signs in for the current power-on session. Runs in the
/// SYSTEM service (firewall changes need admin). Blocks when the gate reports a
/// pending session sign-in and restores access once signed in, the policy is
/// off, or the service stops.
/// </summary>
[SupportedOSPlatform("windows")]
public class LoginEnforcementWorker : BackgroundService
{
    private readonly IActivationGate _gate;
    private readonly ILogger<LoginEnforcementWorker> _logger;
    private bool? _blocked;

    public LoginEnforcementWorker(IActivationGate gate, ILogger<LoginEnforcementWorker> logger)
    {
        _gate = gate;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Login-enforcement worker started.");
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Evaluate();
                await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            // Never strand the device offline: restore access when the service
            // stops (upgrade, reboot, manual stop).
            if (_blocked == true)
            {
                try { InternetBlockHelper.Unblock(_logger); } catch { /* best-effort */ }
            }
        }
    }

    private void Evaluate()
    {
        bool shouldBlock;
        try
        {
            shouldBlock = _gate.NeedsSessionLogin();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not evaluate sign-in state; leaving internet access unchanged.");
            return;
        }

        if (_blocked == shouldBlock)
        {
            return; // Only act on transitions.
        }

        try
        {
            if (shouldBlock)
            {
                InternetBlockHelper.Block(_logger);
            }
            else
            {
                InternetBlockHelper.Unblock(_logger);
            }

            _blocked = shouldBlock;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update internet-block state.");
        }
    }
}
