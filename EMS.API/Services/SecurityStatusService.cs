using EMS.API.DTOs;
using EMS.API.Entities;
using EMS.API.Repositories;

namespace EMS.API.Services;

public class SecurityStatusService : ISecurityStatusService
{
    private readonly IDeviceThreatRepository _threatRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ILogger<SecurityStatusService> _logger;

    public SecurityStatusService(
        IDeviceThreatRepository threatRepository,
        IDeviceRepository deviceRepository,
        ILogger<SecurityStatusService> logger)
    {
        _threatRepository = threatRepository;
        _deviceRepository = deviceRepository;
        _logger = logger;
    }

    public async Task<bool> RecordAsync(
        string deviceId, SecurityStatusReportRequest request, CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetByDeviceIdAsync(deviceId, cancellationToken);
        if (device is null)
        {
            _logger.LogWarning("Security-status report rejected: device {DeviceId} not found.", deviceId);
            return false;
        }

        var utcNow = DateTime.UtcNow;

        device.DefenderRealtimeProtectionEnabled = request.RealtimeProtectionEnabled;
        device.DefenderAntivirusEnabled = request.AntivirusEnabled;
        device.DefenderLastQuickScan = NormalizeUtc(request.LastQuickScan);
        device.DefenderLastFullScan = NormalizeUtc(request.LastFullScan);
        device.DefenderSignatureVersion = Truncate(request.SignatureVersion, 50);
        device.DefenderSignatureAgeDays = request.SignatureAgeDays;
        device.DefenderEngineVersion = Truncate(request.EngineVersion, 50);
        device.SecurityStatusUpdatedAt = utcNow;

        var threats = (request.Threats ?? new List<ThreatDto>())
            .Where(t => !string.IsNullOrWhiteSpace(t.Name))
            .Select(t => new DeviceThreat
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                Name = Truncate(t.Name.Trim(), 400)!,
                Severity = t.Severity,
                DetectedAt = NormalizeUtc(t.DetectedAt),
                Remediated = t.Remediated,
                Resource = Truncate(t.Resource, 1000),
                ReportedAt = utcNow
            })
            .ToList();

        await _threatRepository.ReplaceForDeviceAsync(device.Id, threats, cancellationToken);

        // Both the device mutation and the threat inserts share the one scoped
        // DbContext, so a single save commits them together.
        await _deviceRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Security status updated for device {DeviceId}: realtime={Realtime}, {Count} threat(s).",
            deviceId, request.RealtimeProtectionEnabled, threats.Count);

        return true;
    }

    public async Task<IReadOnlyList<ThreatResponse>?> GetThreatsAsync(
        Guid deviceInternalId, CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceInternalId, cancellationToken);
        if (device is null)
        {
            return null;
        }

        var threats = await _threatRepository.GetByDeviceAsync(deviceInternalId, cancellationToken);

        return threats
            .Select(t => new ThreatResponse
            {
                Id = t.Id,
                Name = t.Name,
                Severity = t.Severity,
                DetectedAt = t.DetectedAt,
                Remediated = t.Remediated,
                Resource = t.Resource
            })
            .ToList();
    }

    /// <summary>Postgres timestamptz needs UTC-kind DateTimes; coerce any incoming kind.</summary>
    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private static string? Truncate(string? value, int maxLength)
        => value is not null && value.Length > maxLength ? value[..maxLength] : value;
}
