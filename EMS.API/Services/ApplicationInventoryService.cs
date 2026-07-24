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
        var blocked = await _repository.GetBlockedAsync(deviceInternalId, cancellationToken);

        var blockedExecutables = blocked
            .Select(b => b.ExecutableName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var response = installed
            .Select(a => ToResponse(a, blockedExecutables))
            .ToList();

        // An app can be blocked without appearing in the current scan (for
        // example it was uninstalled). Surface those so the block is still
        // visible and removable rather than becoming orphaned.
        var installedExecutables = installed
            .Where(a => a.ExecutableName is not null)
            .Select(a => a.ExecutableName!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        response.AddRange(blocked
            .Where(b => !installedExecutables.Contains(b.ExecutableName))
            .Select(b => new InstalledAppResponse
            {
                Id = b.Id,
                Name = b.DisplayName ?? b.ExecutableName,
                ExecutableName = b.ExecutableName,
                IsStoreApp = false,
                IsBlocked = true,
                CanBlock = true
            }));

        return response
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<InstalledAppResponse?> BlockAsync(
        Guid deviceInternalId, BlockApplicationRequest request, CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceInternalId, cancellationToken);
        if (device is null)
        {
            return null;
        }

        var executable = NormalizeExecutable(request.ExecutableName);
        if (executable is null)
        {
            return null;
        }

        var existing = await _repository.GetBlockedByExecutableAsync(
            deviceInternalId, executable, cancellationToken);

        if (existing is null)
        {
            await _repository.AddBlockedAsync(new BlockedApplication
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceInternalId,
                ExecutableName = executable,
                DisplayName = request.DisplayName,
                CreatedDate = DateTime.UtcNow
            }, cancellationToken);

            await _repository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Application {Executable} blocked on device {DeviceId}.", executable, device.DeviceId);
        }

        return new InstalledAppResponse
        {
            Name = request.DisplayName ?? executable,
            ExecutableName = executable,
            IsBlocked = true,
            CanBlock = true
        };
    }

    public async Task<bool> UnblockAsync(
        Guid deviceInternalId, string executableName, CancellationToken cancellationToken = default)
    {
        var executable = NormalizeExecutable(executableName);
        if (executable is null)
        {
            return false;
        }

        var blocked = await _repository.GetBlockedByExecutableAsync(
            deviceInternalId, executable, cancellationToken);

        if (blocked is null)
        {
            return false;
        }

        _repository.RemoveBlocked(blocked);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Application {Executable} unblocked on device {DeviceId}.", executable, deviceInternalId);
        return true;
    }

    private static InstalledAppResponse ToResponse(
        InstalledApplication app, HashSet<string> blockedExecutables) => new()
    {
        Id = app.Id,
        Name = app.Name,
        Version = app.Version,
        Publisher = app.Publisher,
        ExecutableName = app.ExecutableName,
        IsStoreApp = app.IsStoreApp,
        IsBlocked = app.ExecutableName is not null && blockedExecutables.Contains(app.ExecutableName),
        CanBlock = app.ExecutableName is not null
    };

    /// <summary>
    /// Reduces whatever was supplied to a bare lowercase executable name,
    /// which is the key the agent's blocking mechanism uses.
    /// </summary>
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
