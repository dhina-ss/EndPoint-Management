using EMS.API.DTOs;

namespace EMS.API.Services;

public interface IBlockedWebsiteService
{
    /// <summary>Lists a device's custom blocked domains. Returns null if the device does not exist.</summary>
    Task<IReadOnlyList<BlockedWebsiteResponse>?> GetForDeviceAsync(
        Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a domain to a device's block list. Returns the created entry, or a
    /// failure describing why (device missing, invalid domain, duplicate).
    /// </summary>
    Task<AddBlockedWebsiteResult> AddAsync(
        Guid deviceId, string rawDomain, CancellationToken cancellationToken = default);

    /// <summary>Removes a block entry. Returns false if the device or entry does not exist.</summary>
    Task<bool> RemoveAsync(Guid deviceId, Guid blockId, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of an add-domain attempt.</summary>
public sealed record AddBlockedWebsiteResult(
    AddBlockedWebsiteOutcome Outcome, BlockedWebsiteResponse? Created = null, string? Error = null);

public enum AddBlockedWebsiteOutcome
{
    Created,
    DeviceNotFound,
    InvalidDomain,
    Duplicate
}
