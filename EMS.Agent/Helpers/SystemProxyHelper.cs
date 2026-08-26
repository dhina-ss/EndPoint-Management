using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace EMS.Agent.Helpers;

/// <summary>
/// Points the current user's browsers at a local block page by setting the
/// WinINET (Internet Options) proxy, and clears it again. Runs in the user
/// session (per-user HKCU). The agent's own HttpClients disable the proxy, so
/// they keep reaching the server directly.
/// </summary>
[SupportedOSPlatform("windows")]
public static class SystemProxyHelper
{
    private const string SettingsKey = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

    private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
    private const int INTERNET_OPTION_REFRESH = 37;

    /// <summary>Routes browser traffic to <paramref name="proxyAddress"/> (e.g. 127.0.0.1:47624).</summary>
    public static void SetProxy(string proxyAddress, string bypassList)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SettingsKey, writable: true);
            if (key is null)
            {
                return;
            }

            key.SetValue("ProxyServer", proxyAddress, RegistryValueKind.String);
            key.SetValue("ProxyOverride", bypassList, RegistryValueKind.String);
            key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
            Notify();
        }
        catch
        {
            // Best-effort: the firewall block still applies even if this fails.
        }
    }

    public static void ClearProxy()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SettingsKey, writable: true);
            if (key is null)
            {
                return;
            }

            key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
            key.DeleteValue("ProxyServer", throwOnMissingValue: false);
            Notify();
        }
        catch
        {
            // Best-effort.
        }
    }

    private static void Notify()
    {
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
    }
}
