using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace EMS.Agent.Helpers;

/// <summary>
/// Ensures ProgramData\EMS.Agent and its shared state files grant Modify access
/// to BUILTIN\Users (which includes Active Directory Domain Users on domain-joined
/// PCs), so non-admin domain accounts running --usage-tracker or --login can
/// read device-auth.json / device-id.json and update activation/logs cleanly.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ProgramDataPermissions
{
    public static void EnsureUserWritable(ILogger? logger = null)
    {
        try
        {
            var dirPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "EMS.Agent");
            Directory.CreateDirectory(dirPath);

            var usersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var dirInfo = new DirectoryInfo(dirPath);
            var dirSec = dirInfo.GetAccessControl();
            dirSec.AddAccessRule(new FileSystemAccessRule(
                usersSid,
                FileSystemRights.Modify,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            dirInfo.SetAccessControl(dirSec);

            foreach (var file in Directory.GetFiles(dirPath))
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    var fileSec = fileInfo.GetAccessControl();
                    fileSec.AddAccessRule(new FileSystemAccessRule(
                        usersSid,
                        FileSystemRights.Modify,
                        AccessControlType.Allow));
                    fileInfo.SetAccessControl(fileSec);
                }
                catch
                {
                    // Skip files locked exclusively by another process.
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Could not update ProgramData\\EMS.Agent permissions.");
        }
    }
}
