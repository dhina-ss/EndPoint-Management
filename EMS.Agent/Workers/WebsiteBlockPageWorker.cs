using System.Runtime.Versioning;
using EMS.Agent.Services;

namespace EMS.Agent.Workers;

/// <summary>
/// Runs in the SYSTEM service. Establishes the local block-page certificate
/// authority (installing it into the machine Trusted Root store) and serves the
/// branded "site blocked" page on loopback :80/:443, so any domain the hosts
/// file redirects to 127.0.0.1 renders the branded page - over HTTPS without a
/// certificate warning. Binding privileged ports and writing the machine trust
/// store both need the service's SYSTEM rights, which is why this lives here and
/// not in the per-user tracker.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WebsiteBlockPageWorker : BackgroundService
{
    private readonly ILogger<WebsiteBlockPageWorker> _logger;
    private readonly BlockCertificateService _certs;
    private WebsiteBlockServer? _server;

    public WebsiteBlockPageWorker(ILogger<WebsiteBlockPageWorker> logger)
    {
        _logger = logger;
        _certs = new BlockCertificateService(logger);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!_certs.EnsureReady())
            {
                _logger.LogWarning("Block-page CA unavailable; HTTPS block pages will show a browser error instead.");
            }

            _server = new WebsiteBlockServer(_logger, _certs);
            _server.Start();
            _logger.LogInformation("Website block-page server started.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Website block-page server failed to start.");
        }

        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _server?.Stop();
        }
        catch { /* best-effort */ }
        await base.StopAsync(cancellationToken);
    }
}
