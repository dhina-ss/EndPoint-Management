using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace EMS.Agent.Helpers;

/// <summary>
/// Identifies the process currently in the foreground (the app the user is
/// actually looking at), via the Win32 desktop APIs. There is no managed
/// equivalent of GetForegroundWindow.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ForegroundWindowHelper
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>
    /// Process name of the foreground application (e.g. "chrome", "EXCEL"),
    /// or null when there is no foreground window to attribute time to
    /// (locked screen, between window switches, or the process already exited).
    /// </summary>
    public static string? GetForegroundProcessName()
    {
        var windowHandle = GetForegroundWindow();
        if (windowHandle == IntPtr.Zero)
        {
            return null;
        }

        GetWindowThreadProcessId(windowHandle, out var processId);
        if (processId == 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            // The process exited between the handle lookup and GetProcessById.
            return null;
        }
        catch (Win32Exception)
        {
            // Elevated/system windows the agent's account cannot query.
            return null;
        }
    }
}
