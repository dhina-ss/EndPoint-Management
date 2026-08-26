using System.Diagnostics;
using System.Runtime.Versioning;
using EMS.Agent.Helpers;
using EMS.Agent.Services;

namespace EMS.Agent.Workers;

/// <summary>
/// Runs in the per-user tracker. While the require-login policy holds this
/// power-on session (no sign-in yet), it serves the branded block page, points
/// the browser at it via the system proxy, and opens it once. When the user
/// signs in (or the policy is off), it clears the proxy and stops the page.
/// </summary>
[SupportedOSPlatform("windows")]
public class BlockPageWorker : BackgroundService
{
    private readonly IActivationGate _gate;
    private readonly ILogger<BlockPageWorker> _logger;
    private readonly BlockPageServer _server;
    private bool? _active;

    public BlockPageWorker(IActivationGate gate, ILogger<BlockPageWorker> logger)
    {
        _gate = gate;
        _logger = logger;
        _server = new BlockPageServer(logger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Block-page worker started.");
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Evaluate();
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            // Never leave the browser stuck on a dead proxy.
            Deactivate();
        }
    }

    private void Evaluate()
    {
        bool block;
        try
        {
            block = _gate.NeedsSessionLogin();
        }
        catch
        {
            return;
        }

        if (_active == block)
        {
            return;
        }

        if (block)
        {
            Activate();
        }
        else
        {
            Deactivate();
        }

        _active = block;
    }

    private void Activate()
    {
        try
        {
            _server.Start();
            SystemProxyHelper.SetProxy($"127.0.0.1:{BlockPageServer.Port}", "<local>");
            OpenBrowser($"http://127.0.0.1:{BlockPageServer.Port}/");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not activate the block page.");
        }
    }

    private void Deactivate()
    {
        try
        {
            SystemProxyHelper.ClearProxy();
            _server.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not deactivate the block page.");
        }
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* best-effort */ }
    }
}
