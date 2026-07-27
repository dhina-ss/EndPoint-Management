using EMS.API.DTOs;
using EMS.API.Entities;

namespace EMS.API.Services;

/// <summary>Outcome of an upload attempt.</summary>
public enum UploadPackageOutcome
{
    Created,
    InvalidFile,
    UnsupportedType
}

public sealed record UploadPackageResult(
    UploadPackageOutcome Outcome, InstallerPackageResponse? Package, string? Error);

/// <summary>Loaded installer bytes, for streaming to the agent.</summary>
public sealed record PackageContent(byte[] Content, string FileName, string ContentType);

public interface IInstallerPackageService
{
    Task<UploadPackageResult> UploadAsync(
        string fileName, string? displayName, string? silentArgs,
        Stream content, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InstallerPackageResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PackageContent?> GetContentAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a package. Returns false when it is missing or still referenced by a command.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
