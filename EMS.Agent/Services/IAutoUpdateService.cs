namespace EMS.Agent.Services;

public interface IAutoUpdateService
{
    /// <summary>
    /// Checks available update metadata against the current running agent version
    /// and asynchronously downloads and applies the update if a newer version is available.
    /// </summary>
    void CheckAndTriggerUpdate(string? latestVersion, string? downloadUrl, string? expectedSha256);
}
