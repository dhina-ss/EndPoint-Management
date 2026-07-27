using System.Security.Cryptography;
using EMS.API.DTOs;
using EMS.API.Entities;
using EMS.API.Repositories;

namespace EMS.API.Services;

public class InstallerPackageService : IInstallerPackageService
{
    private readonly IInstallerPackageRepository _repository;
    private readonly ILogger<InstallerPackageService> _logger;

    public InstallerPackageService(
        IInstallerPackageRepository repository, ILogger<InstallerPackageService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<UploadPackageResult> UploadAsync(
        string fileName, string? displayName, string? silentArgs,
        Stream content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new UploadPackageResult(UploadPackageOutcome.InvalidFile, null, "A file is required.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var kind = extension switch
        {
            ".msi" => InstallerKind.Msi,
            ".exe" => InstallerKind.Exe,
            _ => (InstallerKind?)null
        };

        if (kind is null)
        {
            return new UploadPackageResult(
                UploadPackageOutcome.UnsupportedType, null, "Only .msi and .exe installers are supported.");
        }

        // Read once into memory, hashing as we go. Pilot-scale: packages are
        // capped by the controller's request-size limit.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        if (bytes.Length == 0)
        {
            return new UploadPackageResult(UploadPackageOutcome.InvalidFile, null, "The uploaded file was empty.");
        }

        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var package = new InstallerPackage
        {
            Id = Guid.NewGuid(),
            FileName = Path.GetFileName(fileName),
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? Path.GetFileNameWithoutExtension(fileName)
                : displayName.Trim(),
            Kind = kind.Value,
            SilentArgs = string.IsNullOrWhiteSpace(silentArgs) ? null : silentArgs.Trim(),
            SizeBytes = bytes.Length,
            Sha256 = sha256,
            Content = bytes,
            UploadedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(package, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Installer package uploaded: {DisplayName} ({FileName}, {Size} bytes).",
            package.DisplayName, package.FileName, package.SizeBytes);

        return new UploadPackageResult(UploadPackageOutcome.Created, ToResponse(package), null);
    }

    public async Task<IReadOnlyList<InstallerPackageResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var packages = await _repository.GetAllMetadataAsync(cancellationToken);
        return packages.Select(ToResponse).ToList();
    }

    public async Task<PackageContent?> GetContentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var package = await _repository.GetWithContentAsync(id, cancellationToken);
        if (package is null)
        {
            return null;
        }

        var contentType = package.Kind == InstallerKind.Msi
            ? "application/x-msi"
            : "application/vnd.microsoft.portable-executable";

        return new PackageContent(package.Content, package.FileName, contentType);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await _repository.IsReferencedByCommandAsync(id, cancellationToken))
        {
            _logger.LogWarning("Refused to delete package {PackageId}: still referenced by a command.", id);
            return false;
        }

        return await _repository.DeleteAsync(id, cancellationToken);
    }

    private static InstallerPackageResponse ToResponse(InstallerPackage p) => new()
    {
        Id = p.Id,
        FileName = p.FileName,
        DisplayName = p.DisplayName,
        Kind = p.Kind.ToString(),
        SilentArgs = p.SilentArgs,
        SizeBytes = p.SizeBytes,
        Sha256 = p.Sha256,
        UploadedAt = p.UploadedAt
    };
}
