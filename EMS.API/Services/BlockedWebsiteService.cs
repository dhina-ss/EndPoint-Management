using EMS.API.DTOs;
using EMS.API.Entities;
using EMS.API.Repositories;

namespace EMS.API.Services;

public class BlockedWebsiteService : IBlockedWebsiteService
{
    private readonly IBlockedWebsiteRepository _repository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ILogger<BlockedWebsiteService> _logger;

    public BlockedWebsiteService(
        IBlockedWebsiteRepository repository,
        IDeviceRepository deviceRepository,
        ILogger<BlockedWebsiteService> logger)
    {
        _repository = repository;
        _deviceRepository = deviceRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BlockedWebsiteResponse>?> GetForDeviceAsync(
        Guid deviceId, CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);
        if (device is null)
        {
            return null;
        }

        var blocks = await _repository.GetByDeviceAsync(deviceId, cancellationToken);
        return blocks.Select(MapToResponse).ToList();
    }

    public async Task<AddBlockedWebsiteResult> AddAsync(
        Guid deviceId, string rawDomain, CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);
        if (device is null)
        {
            return new AddBlockedWebsiteResult(AddBlockedWebsiteOutcome.DeviceNotFound);
        }

        var domain = DomainNormalizer.Normalize(rawDomain);
        if (domain is null)
        {
            return new AddBlockedWebsiteResult(
                AddBlockedWebsiteOutcome.InvalidDomain,
                Error: $"'{rawDomain}' is not a valid website domain.");
        }

        if (await _repository.ExistsAsync(deviceId, domain, cancellationToken))
        {
            return new AddBlockedWebsiteResult(
                AddBlockedWebsiteOutcome.Duplicate,
                Error: $"'{domain}' is already blocked on this device.");
        }

        var entry = new BlockedWebsite
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            Domain = domain,
            CreatedDate = DateTime.UtcNow
        };

        await _repository.AddAsync(entry, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Blocked website {Domain} added for device {DeviceId}.", domain, device.DeviceId);

        return new AddBlockedWebsiteResult(AddBlockedWebsiteOutcome.Created, MapToResponse(entry));
    }

    public async Task<bool> RemoveAsync(Guid deviceId, Guid blockId, CancellationToken cancellationToken = default)
    {
        var entry = await _repository.GetAsync(deviceId, blockId, cancellationToken);
        if (entry is null)
        {
            return false;
        }

        _repository.Remove(entry);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Blocked website {Domain} removed from device {DeviceId}.", entry.Domain, deviceId);
        return true;
    }

    private static BlockedWebsiteResponse MapToResponse(BlockedWebsite entry) => new()
    {
        Id = entry.Id,
        Domain = entry.Domain,
        CreatedDate = entry.CreatedDate
    };
}
