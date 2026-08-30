using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using EMS.Agent.Services;

namespace EMS.Agent.Tray;

/// <summary>
/// The notification-area (tray) icon for the agent. Shows a green check while
/// the device is activated and a red cross while it is not, polling the
/// activation gate so it reflects a sign-in within a few seconds. Runs on its
/// own UI thread inside the per-user tracker process.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class TrayIconContext : ApplicationContext
{
    private readonly IActivationStore _activation;
    private readonly IActivationGate _gate;
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Icon _activatedIcon = TrayIcons.Activated();
    private readonly Icon _notActivatedIcon = TrayIcons.NotActivated();
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _activateItem;
    private bool? _lastActivated;
    private Process? _loginProcess;

    public TrayIconContext(IActivationStore activation, IActivationGate gate)
    {
        _activation = activation;
        _gate = gate;

        _statusItem = new ToolStripMenuItem { Enabled = false };
        _activateItem = new ToolStripMenuItem("Activate EMS…");
        _activateItem.Click += (_, _) => LaunchActivation();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_activateItem);

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Icon = _notActivatedIcon,
            Text = "The Lance Endpoint",
            ContextMenuStrip = menu
        };
        // Double-clicking when inactive opens the activation window.
        _notifyIcon.DoubleClick += (_, _) =>
        {
            if (!IsActivated())
            {
                LaunchActivation();
            }
        };

        _timer = new System.Windows.Forms.Timer { Interval = 5000 };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        Refresh();
    }

    private void Refresh()
    {
        // Require-sign-in-each-startup: when this boot hasn't signed in yet,
        // pop the login window. Re-prompt only after a previous prompt was
        // closed (don't spawn a new window every tick).
        if (NeedsSessionLogin() && (_loginProcess is null || _loginProcess.HasExited))
        {
            _loginProcess = LaunchActivation();
        }

        var activated = IsActivated();
        if (_lastActivated == activated)
        {
            return;
        }

        _lastActivated = activated;
        _notifyIcon.Icon = activated ? _activatedIcon : _notActivatedIcon;

        var who = ActivatedBy();
        _notifyIcon.Text = activated
            ? Trim($"The Lance Endpoint — Activated{(string.IsNullOrWhiteSpace(who) ? "" : $" ({who})")}")
            : "The Lance Endpoint — Not activated";

        _statusItem.Text = activated ? "Status: Activated" : "Status: Not activated";
        _activateItem.Visible = !activated;
    }

    private bool IsActivated()
    {
        try { return _gate.IsMonitoringActive(); }
        catch { return false; }
    }

    private bool NeedsSessionLogin()
    {
        try { return _gate.NeedsSessionLogin(); }
        catch { return false; }
    }

    private string? ActivatedBy()
    {
        try { return _activation.ActivatedBy(); }
        catch { return null; }
    }

    private static Process? LaunchActivation()
    {
        try
        {
            // Don't open a second window if one is already up (installer,
            // block page, or a prior launch) - just bring it forward.
            if (EMS.Agent.Helpers.ActivationInstance.IsOpen())
            {
                EMS.Agent.Helpers.ActivationInstance.BringToFront();
                return null;
            }
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                return Process.Start(new ProcessStartInfo(exe, "--login") { UseShellExecute = false });
            }
        }
        catch
        {
            // Best-effort; the Start Menu shortcut is the fallback.
        }

        return null;
    }

    // NotifyIcon.Text is limited to 63 characters.
    private static string Trim(string value) => value.Length <= 63 ? value : value[..63];

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _activatedIcon.Dispose();
            _notActivatedIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
