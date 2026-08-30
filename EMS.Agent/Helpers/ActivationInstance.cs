using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace EMS.Agent.Helpers;

/// <summary>
/// Ensures only one activation window is open at a time. That window can be
/// launched by the installer (post-install), the tray icon, the block page, or
/// the Start Menu shortcut - without this guard, several of those firing
/// together (e.g. right after install) open multiple activation windows.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ActivationInstance
{
    // Session-local: every activation window runs in the interactive user session.
    private const string MutexName = "TheLanceEndpoint-Activation-Window";

    /// <summary>
    /// Acquires the single-instance lock. Returns the held mutex when this
    /// process is the sole owner (keep it referenced for the window's lifetime),
    /// or null when an activation window is already open elsewhere.
    /// </summary>
    public static Mutex? Acquire()
    {
        var mutex = new Mutex(true, MutexName, out var createdNew);
        if (createdNew)
        {
            return mutex;
        }
        mutex.Dispose();
        return null;
    }

    /// <summary>True when an activation window is currently open in this session.</summary>
    public static bool IsOpen()
    {
        try
        {
            using var existing = Mutex.OpenExisting(MutexName);
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Brings the already-open activation window to the foreground.</summary>
    public static void BringToFront()
    {
        try
        {
            var me = Process.GetCurrentProcess();
            foreach (var p in Process.GetProcessesByName(me.ProcessName))
            {
                if (p.Id == me.Id)
                {
                    continue;
                }
                var handle = p.MainWindowHandle;
                if (handle != IntPtr.Zero)
                {
                    ShowWindow(handle, SW_RESTORE);
                    SetForegroundWindow(handle);
                    return;
                }
            }
        }
        catch
        {
            // Best-effort.
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;
}
