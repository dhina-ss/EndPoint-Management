namespace EMS.Agent.Models;

/// <summary>
/// One application discovered on the machine.
/// </summary>
public class InstalledAppModel
{
    public string Name { get; set; } = string.Empty;

    public string? Version { get; set; }

    public string? Publisher { get; set; }

    /// <summary>
    /// Main executable file name (e.g. "chrome.exe") where it could be
    /// determined. Blocking keys off this, so apps without it can be listed
    /// but not blocked.
    /// </summary>
    public string? ExecutableName { get; set; }

    /// <summary>True for Microsoft Store / built-in UWP apps.</summary>
    public bool IsStoreApp { get; set; }
}
