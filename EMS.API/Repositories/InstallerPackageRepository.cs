using EMS.API.Data;
using EMS.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace EMS.API.Repositories;

public class InstallerPackageRepository : IInstallerPackageRepository
{
    private readonly ApplicationDbContext _dbContext;

    public InstallerPackageRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(InstallerPackage package, CancellationToken cancellationToken = default)
    {
        await _dbContext.InstallerPackages.AddAsync(package, cancellationToken);
    }

    public async Task<IReadOnlyList<InstallerPackage>> GetAllMetadataAsync(CancellationToken cancellationToken = default)
    {
        // Projection drops Content so the (potentially large) bytea is never
        // pulled into memory just to list packages.
        return await _dbContext.InstallerPackages
            .AsNoTracking()
            .OrderByDescending(p => p.UploadedAt)
            .Select(p => new InstallerPackage
            {
                Id = p.Id,
                FileName = p.FileName,
                DisplayName = p.DisplayName,
                Kind = p.Kind,
                SilentArgs = p.SilentArgs,
                SizeBytes = p.SizeBytes,
                Sha256 = p.Sha256,
                UploadedAt = p.UploadedAt,
                Content = Array.Empty<byte>()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<InstallerPackage?> GetMetadataByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.InstallerPackages
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new InstallerPackage
            {
                Id = p.Id,
                FileName = p.FileName,
                DisplayName = p.DisplayName,
                Kind = p.Kind,
                SilentArgs = p.SilentArgs,
                SizeBytes = p.SizeBytes,
                Sha256 = p.Sha256,
                UploadedAt = p.UploadedAt,
                Content = Array.Empty<byte>()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<InstallerPackage?> GetWithContentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.InstallerPackages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<bool> IsReferencedByCommandAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DeviceCommands
            .AnyAsync(c => c.PackageId == id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await _dbContext.InstallerPackages
            .Where(p => p.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
