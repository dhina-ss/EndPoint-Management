using EMS.API.DTOs;
using EMS.API.Entities;
using EMS.API.Repositories;

namespace EMS.API.Services;

public class ApplicationInventoryService : IApplicationInventoryService
{
    private readonly IApplicationInventoryRepository _repository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ILogger<ApplicationInventoryService> _logger;

    public ApplicationInventoryService(
        IApplicationInventoryRepository repository,
        IDeviceRepository deviceRepository,
        ILogger<ApplicationInventoryService> logger)
    {
        _repository = repository;
        _deviceRepository = deviceRepository;
        _logger = logger;
    }

    public async Task<bool> ReplaceInventoryAsync(
        string deviceId, InstalledAppsReportRequest request, CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetByDeviceIdAsync(deviceId, cancellationToken);
        if (device is null)
        {
            _logger.LogWarning("Installed-apps report rejected: device {DeviceId} not found.", deviceId);
            return false;
        }

        var utcNow = DateTime.UtcNow;
        var applications = request.Applications
            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
            .Select(a => new InstalledApplication
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                Name = a.Name.Trim(),
                Version = a.Version,
                Publisher = a.Publisher,
                ExecutableName = NormalizeExecutable(a.ExecutableName),
                IsStoreApp = a.IsStoreApp,
                ReportedAt = utcNow
            })
            .ToList();

        await _repository.ReplaceInstalledAsync(device.Id, applications, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Installed-apps inventory updated for device {DeviceId}: {Count} application(s).",
            deviceId, applications.Count);

        return true;
    }

    public async Task<IReadOnlyList<InstalledAppResponse>?> GetInventoryAsync(
        Guid deviceInternalId, CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceInternalId, cancellationToken);
        if (device is null)
        {
            return null;
        }

        var installed = await _repository.GetInstalledAsync(deviceInternalId, cancellationToken);

        return installed
            .Select(ToResponse)
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static InstalledAppResponse ToResponse(InstalledApplication app) => new()
    {
        Id = app.Id,
        Name = app.Name,
        Version = app.Version,
        Publisher = app.Publisher,
        ExecutableName = app.ExecutableName,
        IsStoreApp = app.IsStoreApp
    };

    /// <summary>Reduces whatever was supplied to a bare lowercase executable name.</summary>
    private static string? NormalizeExecutable(string? raw)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            value = Path.GetFileName(value);
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrEmpty(value) || !value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value.ToLowerInvariant();
    }
}
