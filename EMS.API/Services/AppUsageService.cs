using EMS.API.DTOs;
using EMS.API.Entities;
using EMS.API.Repositories;

namespace EMS.API.Services;

public class AppUsageService : IAppUsageService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IAppUsageRepository _appUsageRepository;
    private readonly ILogger<AppUsageService> _logger;

    public AppUsageService(
        IDeviceRepository deviceRepository,
        IAppUsageRepository appUsageRepository,
        ILogger<AppUsageService> logger)
    {
        _deviceRepository = deviceRepository;
        _appUsageRepository = appUsageRepository;
        _logger = logger;
    }

    public async Task<AppUsageReportResponse?> RecordUsageAsync(
        string deviceId, AppUsageReportRequest request, CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetByDeviceIdAsync(deviceId, cancellationToken);
        if (device is null)
        {
            _logger.LogWarning("App usage report rejected: device {DeviceId} not found.", deviceId);
            return null;
        }

        var usageDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var utcNow = DateTime.UtcNow;

        foreach (var entry in request.UsageRecords)
        {
            if (entry.DurationSeconds <= 0)
            {
                continue;
            }

            var record = await _appUsageRepository.GetTrackedAsync(
                device.Id, entry.ApplicationName, usageDate, cancellationToken);

            if (record is null)
            {
                record = new AppUsageRecord
                {
                    Id = Guid.NewGuid(),
                    DeviceId = device.Id,
                    ApplicationName = entry.ApplicationName,
                    UsageDate = usageDate,
                    DurationSeconds = entry.DurationSeconds,
                    LastUpdated = utcNow
                };
                await _appUsageRepository.AddAsync(record, cancellationToken);
            }
            else
            {
                record.DurationSeconds += entry.DurationSeconds;
                record.LastUpdated = utcNow;
            }
        }

        await _appUsageRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Recorded usage for {Count} application(s) on device {DeviceId}.",
            request.UsageRecords.Count, deviceId);

        return new AppUsageReportResponse { Success = true, Message = "App usage recorded" };
    }

    public async Task<IReadOnlyList<AppUsageSummaryResponse>> GetUsageAsync(
        Guid deviceInternalId, DateOnly usageDate, CancellationToken cancellationToken = default)
    {
        var records = await _appUsageRepository.GetByDeviceAndDateAsync(deviceInternalId, usageDate, cancellationToken);

        return records.Select(r => new AppUsageSummaryResponse
        {
            ApplicationName = r.ApplicationName,
            DurationSeconds = r.DurationSeconds,
            UsageDate = r.UsageDate
        }).ToList();
    }
}
