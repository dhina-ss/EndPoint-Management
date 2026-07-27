namespace EMS.API.Entities;

/// <summary>How an installer package is executed on the endpoint.</summary>
public enum InstallerKind
{
    /// <summary>Windows Installer database, run via <c>msiexec /i ... /qn</c>.</summary>
    Msi = 0,

    /// <summary>Stand-alone executable, run with admin-supplied silent switches.</summary>
    Exe = 1
}

/// <summary>
/// An installer (MSI/EXE) uploaded once to the server and reusable across
/// devices. The bytes live in the database for the pilot so the deployment
/// stays self-contained on Neon (Render's filesystem is ephemeral); production
/// should move <see cref="Content"/> to object storage and keep only a pointer.
/// </summary>
public class InstallerPackage
{
    public Guid Id { get; set; }

    /// <summary>Original uploaded file name, e.g. "7z2408-x64.msi".</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Friendly name shown in the dashboard.</summary>
    public string DisplayName { get; set; } = string.Empty;

    public InstallerKind Kind { get; set; }

    /// <summary>
    /// Silent-install switches for an EXE (e.g. "/S", "/silent /norestart").
    /// Ignored for MSI, which always uses "/qn /norestart".
    /// </summary>
    public string? SilentArgs { get; set; }

    public long SizeBytes { get; set; }

    /// <summary>Lower-case hex SHA-256 the agent verifies after download.</summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>The raw installer bytes (Postgres bytea).</summary>
    public byte[] Content { get; set; } = Array.Empty<byte>();

    public DateTime UploadedAt { get; set; }
}
