using System.Text.Json;

namespace EMS.Agent.Services;

/// <summary>
/// Persistent token storage in ProgramData\EMS.Agent\device-auth.json.
/// The server rotates the token on every registration, so the file always
/// holds the latest issued credential; a write failure degrades gracefully
/// to in-memory (heartbeats keep working until the next restart).
/// </summary>
public class DeviceTokenService : IDeviceTokenService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly IDeviceIdService _deviceIdService;
    private readonly ILogger<DeviceTokenService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private volatile bool _loaded;
    private string? _cachedToken;

    public DeviceTokenService(IDeviceIdService deviceIdService, ILogger<DeviceTokenService> logger)
        : this(deviceIdService, logger, DefaultFilePath())
    {
    }

    /// <summary>Overload for tests; production code uses the default path.</summary>
    public DeviceTokenService(IDeviceIdService deviceIdService, ILogger<DeviceTokenService> logger, string filePath)
    {
        _deviceIdService = deviceIdService;
        _logger = logger;
        _filePath = filePath;
    }

    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
        {
            return _cachedToken;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_loaded)
            {
                _cachedToken = await TryReadAsync(cancellationToken);
                _loaded = true;
            }

            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveTokenAsync(string deviceId, string token, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _cachedToken = token;
            _loaded = true;

            await TryPersistAsync(deviceId, token, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }

    private static string DefaultFilePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "EMS.Agent",
            "device-auth.json");
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
            var document = await JsonSerializer.DeserializeAsync<AuthDocument>(
                stream, cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(document?.Token))
            {
                _logger.LogWarning("Device auth file {Path} has no token; a new one arrives with the next registration.", _filePath);
                return null;
            }

            // A credential minted for a different device identity (e.g. after
            // device-id.json was reset) must not be reused.
            var currentDeviceId = await _deviceIdService.GetDeviceIdAsync(cancellationToken);
            if (!string.Equals(document.DeviceId, currentDeviceId, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Stored token belongs to device {StoredDeviceId} but this agent is {DeviceId}; ignoring it.",
                    document.DeviceId, currentDeviceId);
                return null;
            }

            _logger.LogInformation("Loaded device token from {Path}.", _filePath);
            return document.Token;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to read device auth file {Path}; continuing without a stored token.", _filePath);
            return null;
        }
    }

    private async Task TryPersistAsync(string deviceId, string token, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(
                stream,
                new AuthDocument { DeviceId = deviceId, Token = token },
                SerializerOptions,
                cancellationToken);

            _logger.LogInformation("Device token persisted to {Path}.", _filePath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Failed to persist device token to {Path}; heartbeats work this session but not across a restart.",
                _filePath);
        }
    }

    private sealed class AuthDocument
    {
        public string? DeviceId { get; set; }

        public string? Token { get; set; }
    }
}
