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
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Icon _activatedIcon = TrayIcons.Activated();
    private readonly Icon _notActivatedIcon = TrayIcons.NotActivated();
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _activateItem;
    private bool? _lastActivated;

    public TrayIconContext(IActivationStore activation)
    {
        _activation = activation;

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
            Text = "EMS Agent",
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
        var activated = IsActivated();
        if (_lastActivated == activated)
        {
            return;
        }

        _lastActivated = activated;
        _notifyIcon.Icon = activated ? _activatedIcon : _notActivatedIcon;

        var who = ActivatedBy();
        _notifyIcon.Text = activated
            ? Trim($"EMS Agent — Activated{(string.IsNullOrWhiteSpace(who) ? "" : $" ({who})")}")
            : "EMS Agent — Not activated";

        _statusItem.Text = activated ? "Status: Activated" : "Status: Not activated";
        _activateItem.Visible = !activated;
    }

    private bool IsActivated()
    {
        try { return _activation.IsActivated(); }
        catch { return false; }
    }

    private string? ActivatedBy()
    {
        try { return _activation.ActivatedBy(); }
        catch { return null; }
    }

    private static void LaunchActivation()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                Process.Start(new ProcessStartInfo(exe, "--login") { UseShellExecute = false });
            }
        }
        catch
        {
            // Best-effort; the Start Menu shortcut is the fallback.
        }
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
