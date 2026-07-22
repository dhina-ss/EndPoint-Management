using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace EMS.Agent.Helpers;

/// <summary>
/// The Worker Service SDK builds a console-subsystem executable, which is
/// fine for the Windows Service (the Service Control Manager never attaches
/// a console regardless of subsystem) but means a visible console window
/// pops up when the usage-tracker mode is launched directly by the
/// Scheduled Task. That mode logs to a file, not the console, so the
/// console serves no purpose there and is hidden.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ConsoleWindowHelper
{
    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    public static void DetachConsole()
    {
        try
        {
            FreeConsole();
        }
        catch
        {
            // Best-effort: a visible console window is cosmetic, not fatal.
        }
    }
}
