using System.Runtime.Versioning;
using EMS.Agent.Services;
using EMS.Agent.Tray;

namespace EMS.Agent.Workers;

/// <summary>
/// Hosts the notification-area icon on a dedicated STA UI thread inside the
/// per-user tracker process (which runs in the interactive session, where a
/// tray icon can appear). The SYSTEM service has no desktop, so this cannot
/// live there.
/// </summary>
[SupportedOSPlatform("windows")]
public class TrayIconWorker : BackgroundService
{
    private readonly IActivationStore _activation;
    private readonly ILogger<TrayIconWorker> _logger;

    public TrayIconWorker(IActivationStore activation, ILogger<TrayIconWorker> logger)
    {
        _activation = activation;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var uiThread = new Thread(RunTray) { IsBackground = true, Name = "EMS Tray" };
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start();

        // End the tray's message loop when the host shuts down.
        stoppingToken.Register(() =>
        {
            try
            {
                System.Windows.Forms.Application.Exit();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not signal the tray to exit.");
            }
        });

        return Task.CompletedTask;
    }

    private void RunTray()
    {
        try
        {
            using var context = new TrayIconContext(_activation);
            System.Windows.Forms.Application.Run(context);
        }
        catch (Exception ex)
        {
            // A missing desktop or GDI failure must not take down usage tracking.
            _logger.LogWarning(ex, "Tray icon could not start; continuing without it.");
        }
    }
}
