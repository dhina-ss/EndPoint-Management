namespace EMS.Agent.Services;

/// <summary>
/// Reads the license key the installer dropped from the download bundle
/// (ProgramData\EMS.Agent\license.key). Lets the agent learn its tenant's
/// policy before the user has activated.
/// </summary>
public static class LicenseKeyFile
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "EMS.Agent", "license.key");

    public static string? Read()
    {
        try
        {
            return File.Exists(FilePath) ? File.ReadAllText(FilePath).Trim() : null;
        }
        catch
        {
            return null;
        }
    }
}
