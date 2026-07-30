using EMS.API.DTOs;
using EMS.API.Entities;
using EMS.API.Repositories;

namespace EMS.API.Services;

public class WorkSessionService : IWorkSessionService
{
    private readonly IWorkSessionRepository _workSessions;
    private readonly IDeviceRepository _devices;
    private readonly ILogger<WorkSessionService> _logger;

    public WorkSessionService(
        IWorkSessionRepository workSessions,
        IDeviceRepository devices,
        ILogger<WorkSessionService> logger)
    {
        _workSessions = workSessions;
        _devices = devices;
        _logger = logger;
    }

    public async Task<bool> RecordAsync(
        string deviceId, WorkTimeReportRequest request, CancellationToken cancellationToken = default)
    {
        var device = await _devices.GetByDeviceIdAsync(deviceId, cancellationToken);
        if (device is null)
        {
            _logger.LogWarning("Work-time report rejected: device {DeviceId} not found.", deviceId);
            return false;
        }

        var utcNow = DateTime.UtcNow;

        foreach (var delta in request.Sessions)
        {
            if (delta.SecondsDelta <= 0)
            {
                continue;
            }

            var record = await _workSessions.GetTrackedAsync(device.Id, delta.WorkDate, cancellationToken);
            if (record is null)
            {
                await _workSessions.AddAsync(new WorkSessionRecord
                {
                    Id = Guid.NewGuid(),
                    DeviceId = device.Id,
                    WorkDate = delta.WorkDate,
                    WorkedSeconds = delta.SecondsDelta,
                    LastUpdated = utcNow
                }, cancellationToken);
            }
            else
            {
                record.WorkedSeconds += delta.SecondsDelta;
                record.LastUpdated = utcNow;
            }
        }

        await _workSessions.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<WorkTimeResponse>?> GetForDeviceAsync(
        Guid deviceInternalId, int days, CancellationToken cancellationToken = default)
    {
        var device = await _devices.GetByIdAsync(deviceInternalId, cancellationToken);
        if (device is null)
        {
            return null;
        }

        var fromDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-(Math.Max(1, days) - 1));
        var records = await _workSessions.GetByDeviceSinceAsync(deviceInternalId, fromDate, cancellationToken);

        return records
            .Select(r => new WorkTimeResponse { WorkDate = r.WorkDate, WorkedSeconds = r.WorkedSeconds })
            .ToList();
    }

    public async Task<bool> SetPowerStateAsync(
        string deviceId, bool suspended, CancellationToken cancellationToken = default)
    {
        // GetByDeviceIdAsync returns a tracked entity, so the change persists.
        var device = await _devices.GetByDeviceIdAsync(deviceId, cancellationToken);
        if (device is null)
        {
            return false;
        }

        device.SuspendedAt = suspended ? DateTime.UtcNow : null;
        await _devices.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Device {DeviceId} power state: {State}.", deviceId, suspended ? "suspended" : "resumed");
        return true;
    }
}
