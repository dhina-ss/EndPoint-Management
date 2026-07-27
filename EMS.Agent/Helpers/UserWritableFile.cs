namespace EMS.Agent.Helpers;

/// <summary>
/// Writes files under ProgramData\EMS.Agent that a standard (non-admin) user
/// session must be able to update. If an earlier elevated run left the file
/// owned by Administrators and read-only for Users, a normal overwrite is
/// denied; since the agent owns the containing folder, we delete the stale
/// file and recreate it (the fresh file inherits the folder's writable ACL).
/// </summary>
public static class UserWritableFile
{
    public static void WriteAllText(string path, string content)
    {
        try
        {
            File.WriteAllText(path, content);
        }
        catch (UnauthorizedAccessException)
        {
            File.Delete(path);
            File.WriteAllText(path, content);
        }
    }
}
