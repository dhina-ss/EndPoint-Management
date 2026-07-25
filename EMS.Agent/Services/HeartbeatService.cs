using System.Reflection;
using EMS.Agent.Helpers;
using EMS.Agent.Models;

namespace EMS.Agent.Services;

/// <summary>
/// Assembles the heartbeat payload (current IP, logged-on user, agent
/// version) and hands it to the API client. Payload detail is best-effort:
/// liveness matters more than completeness.
/// </summary>
public class HeartbeatService : IHeartbeatService
{
    private static readonly string AgentVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    private readonly IApiClientService _apiClient;
    private readonly ISystemMetricsService _metricsService;
    private readonly IStoreUnlockStore _storeUnlockStore;
    private readonly ILogger<HeartbeatService> _logger;

    public HeartbeatService(
        IApiClientService apiClient,
        ISystemMetricsService metricsService,
        IStoreUnlockStore storeUnlockStore,
        ILogger<HeartbeatService> logger)
    {
        _apiClient = apiClient;
        _metricsService = metricsService;
        _storeUnlockStore = storeUnlockStore;
        _logger = logger;
    }

    public async Task<bool> SendHeartbeatAsync(CancellationToken cancellationToken = default)
    {
        var heartbeat = BuildHeartbeat();
        var outcome = await _apiClient.SendHeartbeatAsync(heartbeat, cancellationToken);

        if (outcome.Success)
        {
            UsbBlockingHelper.ApplyPolicy(outcome.UsbBlockingEnabled, _logger);

            // Always-on default phishing/malware list plus this device's
            // custom domains from the server.
            var domainsToBlock = PhishingBlocklist.Domains
                .Concat(outcome.BlockedWebsites)
                .ToList();
            HostsFileHelper.ApplyBlocklist(domainsToBlock, _logger);

            ApplyStorePolicy(outcome.StoreGatingEnabled);
        }

        return outcome.Success;
    }

    /// <summary>
    /// Keeps the Microsoft Store disabled while gating is enabled, except
    /// during an active local unlock. When gating is off, the Store is left
    /// enabled.
    /// </summary>
    private void ApplyStorePolicy(bool storeGatingEnabled)
    {
        if (!storeGatingEnabled)
        {
            StoreControlHelper.EnableStore(_logger);
            return;
        }

        if (_storeUnlockStore.IsUnlockActive())
        {
            StoreControlHelper.EnableStore(_logger);
        }
        else
        {
            StoreControlHelper.DisableStore(_logger);
        }
    }

    private HeartbeatModel BuildHeartbeat()
    {
        var heartbeat = new HeartbeatModel
        {
            Username = Environment.UserName,
            AgentVersion = AgentVersion
        };

        try
        {
            var (ipAddress, _) = SystemInfoHelper.GetPrimaryNetworkInfo();
            heartbeat.IPAddress = ipAddress;

            var (_, _, _, loggedOnUser) = SystemInfoHelper.GetComputerSystemInfo();
            if (!string.IsNullOrWhiteSpace(loggedOnUser))
            {
                heartbeat.Username = loggedOnUser;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not collect heartbeat details; sending partial payload.");
        }

        // Live monitoring rides along with the heartbeat rather than running
        // its own uploader: the cadence (60s) is already what live metrics need.
        try
        {
            heartbeat.Metrics = _metricsService.Collect();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not collect system metrics; sending heartbeat without them.");
        }

        return heartbeat;
    }
}
