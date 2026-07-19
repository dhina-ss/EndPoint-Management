using System.Text.Json;

namespace EMS.Agent.Services;

/// <summary>
/// Persistent device identity. On first run a GUID is generated and stored in
/// device-id.json; every later run (and reinstall that keeps ProgramData) reads
/// the same id back, so the server never sees this machine as a new device.
/// </summary>
public class DeviceIdService : IDeviceIdService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly ILogger<DeviceIdService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile string? _cachedDeviceId;

    public DeviceIdService(ILogger<DeviceIdService> logger)
        : this(logger, DefaultFilePath())
    {
    }

    /// <summary>Overload for tests; production code uses the default path.</summary>
    public DeviceIdService(ILogger<DeviceIdService> logger, string filePath)
    {
        _logger = logger;
        _filePath = filePath;
    }

    public async Task<string> GetDeviceIdAsync(CancellationToken cancellationToken = default)
    {
        // Fast path: already resolved for this process.
        var cached = _cachedDeviceId;
        if (cached is not null)
        {
            return cached;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            _cachedDeviceId ??= await LoadOrCreateAsync(cancellationToken);
            return _cachedDeviceId;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public void Dispose()
    {
        _initLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private static string DefaultFilePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "EMS.Agent",
            "device-id.json");
    }

    private async Task<string> LoadOrCreateAsync(CancellationToken cancellationToken)
    {
        var existingId = await TryReadAsync(cancellationToken);
        if (existingId is not null)
        {
            _logger.LogInformation("Loaded existing DeviceId {DeviceId} from {Path}", existingId, _filePath);
            return existingId;
        }

        var newId = Guid.NewGuid().ToString();
        _logger.LogInformation("Generated new DeviceId {DeviceId}", newId);

        await TryPersistAsync(newId, cancellationToken);
        return newId;
    }

    private async Task<string?> TryReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            await using var stream = File.OpenRead(_filePath);
            var document = await JsonSerializer.DeserializeAsync<DeviceIdDocument>(
                stream, cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(document?.DeviceId))
            {
                _logger.LogWarning("Device id file {Path} has no DeviceId; a new one will be generated.", _filePath);
                return null;
            }

            return document.DeviceId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to read device id file {Path}; a new DeviceId will be generated.", _filePath);
            return null;
        }
    }

    private async Task TryPersistAsync(string deviceId, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(
                stream, new DeviceIdDocument { DeviceId = deviceId }, SerializerOptions, cancellationToken);

            _logger.LogInformation("DeviceId persisted to {Path}", _filePath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The in-memory id keeps this session working, but the next start
            // would register as a new device — surface that loudly.
            _logger.LogError(ex, "Failed to persist DeviceId to {Path}; the id will not survive a restart.", _filePath);
        }
    }

    private sealed class DeviceIdDocument
    {
        public string? DeviceId { get; set; }
    }
}
