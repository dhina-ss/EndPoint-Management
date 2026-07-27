using EMS.API.Entities;

namespace EMS.API.Repositories;

/// <summary>Storage for uploaded installer packages (MSI/EXE).</summary>
public interface IInstallerPackageRepository
{
    Task AddAsync(InstallerPackage package, CancellationToken cancellationToken = default);

    /// <summary>All packages, metadata only (never loads the byte content).</summary>
    Task<IReadOnlyList<InstallerPackage>> GetAllMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>Metadata for one package (no content), or null if missing.</summary>
    Task<InstallerPackage?> GetMetadataByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Full package including <see cref="InstallerPackage.Content"/>, for the agent download.</summary>
    Task<InstallerPackage?> GetWithContentAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>True if any command still references this package.</summary>
    Task<bool> IsReferencedByCommandAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
