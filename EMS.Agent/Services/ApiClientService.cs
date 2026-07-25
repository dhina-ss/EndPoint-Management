using System.Net;
using System.Net.Http.Json;
using EMS.Agent.Configuration;
using EMS.Agent.Models;
using EMS.Shared.Constants;
using Microsoft.Extensions.Options;

namespace EMS.Agent.Services;

/// <summary>
/// REST client for EMS.API. Registration retries transient failures (network
/// errors, timeouts, 5xx) with a linearly growing delay; heartbeats are
/// single-shot because the next scheduled heartbeat is the natural retry.
/// The token returned by each registration is stored via
/// <see cref="IDeviceTokenService"/> and attached to authenticated calls.
/// </summary>
public class ApiClientService : IApiClientService
{
    private readonly HttpClient _httpClient;
    private readonly ApiSettings _settings;
    private readonly IDeviceTokenService _tokenService;
    private readonly IDeviceIdService _deviceIdService;
    private readonly ILogger<ApiClientService> _logger;

    public ApiClientService(
        HttpClient httpClient,
        IOptions<ApiSettings> settings,
        IDeviceTokenService tokenService,
        IDeviceIdService deviceIdService,
        ILogger<ApiClientService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _tokenService = tokenService;
        _deviceIdService = deviceIdService;
        _logger = logger;
    }

    public async Task<bool> RegisterDeviceAsync(DeviceInventoryModel inventory, CancellationToken cancellationToken = default)
    {
        var maxAttempts = Math.Max(1, _settings.MaxRetryAttempts);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var outcome = await TrySendRegistrationAsync(inventory, attempt, maxAttempts, cancellationToken);
                if (outcome is not null)
                {
                    return outcome.Value;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex,
                    "Attempt {Attempt}/{MaxAttempts}: could not reach the EMS server at {BaseUrl}.",
                    attempt, maxAttempts, _httpClient.BaseAddress);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // HttpClient signals its timeout as a cancellation.
                _logger.LogWarning(
                    "Attempt {Attempt}/{MaxAttempts}: request timed out after {TimeoutSeconds}s.",
                    attempt, maxAttempts, _settings.TimeoutSeconds);
            }

            if (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds((double)_settings.RetryDelaySeconds * attempt);
                await Task.Delay(delay, cancellationToken);
            }
        }

        _logger.LogError("Device registration failed after {MaxAttempts} attempts.", maxAttempts);
        return false;
    }

    public async Task<HeartbeatOutcome> SendHeartbeatAsync(HeartbeatModel heartbeat, CancellationToken cancellationToken = default)
    {
        var token = await _tokenService.GetTokenAsync(cancellationToken);
        if (token is null)
        {
            _logger.LogDebug("Heartbeat skipped: agent has not registered yet.");
            return HeartbeatOutcome.Failed;
        }

        var deviceId = await _deviceIdService.GetDeviceIdAsync(cancellationToken);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _settings.HeartbeatEndpoint)
            {
                Content = JsonContent.Create(heartbeat)
            };
            request.Headers.Add(DeviceAuthHeaders.DeviceId, deviceId);
            request.Headers.Add(DeviceAuthHeaders.Token, token);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<HeartbeatResult>(cancellationToken);
                _logger.LogDebug("Heartbeat acknowledged. Server time: {ServerTime}", result?.ServerTime);
                return new HeartbeatOutcome(
                    true,
                    result?.UsbBlockingEnabled ?? false,
                    result?.BlockedWebsites ?? Array.Empty<string>());
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Token rotated away (e.g. another registration raced us);
                // the next registration cycle stores a fresh one.
                _logger.LogWarning("Heartbeat rejected as unauthorized; waiting for the next registration to refresh the token.");
                return HeartbeatOutcome.Failed;
            }

            _logger.LogWarning("Heartbeat failed with status {StatusCode}.", (int)response.StatusCode);
            return HeartbeatOutcome.Failed;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Heartbeat could not reach the EMS server at {BaseUrl}.", _httpClient.BaseAddress);
            return HeartbeatOutcome.Failed;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Heartbeat timed out after {TimeoutSeconds}s.", _settings.TimeoutSeconds);
            return HeartbeatOutcome.Failed;
        }
    }

    public async Task<bool> SendAppUsageAsync(IReadOnlyList<AppUsageModel> usage, CancellationToken cancellationToken = default)
    {
        if (usage.Count == 0)
        {
            return true;
        }

        var token = await _tokenService.GetTokenAsync(cancellationToken);
        if (token is null)
        {
            _logger.LogDebug("App usage upload skipped: agent has not registered yet.");
            return false;
        }

        var deviceId = await _deviceIdService.GetDeviceIdAsync(cancellationToken);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _settings.AppUsageEndpoint)
            {
                Content = JsonContent.Create(new AppUsageReportPayload { UsageRecords = usage.ToList() })
            };
            request.Headers.Add(DeviceAuthHeaders.DeviceId, deviceId);
            request.Headers.Add(DeviceAuthHeaders.Token, token);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("App usage upload rejected as unauthorized; waiting for the next registration to refresh the token.");
                return false;
            }

            _logger.LogWarning("App usage upload failed with status {StatusCode}.", (int)response.StatusCode);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "App usage upload could not reach the EMS server at {BaseUrl}.", _httpClient.BaseAddress);
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("App usage upload timed out after {TimeoutSeconds}s.", _settings.TimeoutSeconds);
            return false;
        }
    }

    public async Task<bool> SendInstalledAppsAsync(
        IReadOnlyList<InstalledAppModel> applications, CancellationToken cancellationToken = default)
    {
        var token = await _tokenService.GetTokenAsync(cancellationToken);
        if (token is null)
        {
            _logger.LogDebug("Installed-apps report skipped: agent has not registered yet.");
            return false;
        }

        var deviceId = await _deviceIdService.GetDeviceIdAsync(cancellationToken);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _settings.InstalledAppsEndpoint)
            {
                Content = JsonContent.Create(new InstalledAppsPayload { Applications = applications.ToList() })
            };
            request.Headers.Add(DeviceAuthHeaders.DeviceId, deviceId);
            request.Headers.Add(DeviceAuthHeaders.Token, token);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            _logger.LogWarning(
                "Installed-apps report failed with status {StatusCode}.", (int)response.StatusCode);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,
                "Installed-apps report could not reach the EMS server at {BaseUrl}.", _httpClient.BaseAddress);
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Installed-apps report timed out after {TimeoutSeconds}s.", _settings.TimeoutSeconds);
            return false;
        }
    }

    /// <summary>
    /// Sends one registration request. Returns the final result, or null when
    /// the failure is transient and the attempt should be retried.
    /// </summary>
    private async Task<bool?> TrySendRegistrationAsync(
        DeviceInventoryModel inventory, int attempt, int maxAttempts, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Registering device {DeviceId} with the EMS server (attempt {Attempt}/{MaxAttempts}).",
            inventory.DeviceId, attempt, maxAttempts);

        using var request = new HttpRequestMessage(HttpMethod.Post, _settings.RegisterEndpoint)
        {
            Content = JsonContent.Create(inventory)
        };

        // Send credentials when we already have them so every request after
        // first contact is authenticated; the very first registration is
        // necessarily anonymous - it is the call that issues the token.
        var existingToken = await _tokenService.GetTokenAsync(cancellationToken);
        if (existingToken is not null)
        {
            request.Headers.Add(DeviceAuthHeaders.DeviceId, inventory.DeviceId);
            request.Headers.Add(DeviceAuthHeaders.Token, existingToken);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<RegistrationResult>(cancellationToken);

            if (result?.Success == true)
            {
                if (!string.IsNullOrWhiteSpace(result.Token))
                {
                    await _tokenService.SaveTokenAsync(
                        result.DeviceId ?? inventory.DeviceId, result.Token, cancellationToken);
                }

                _logger.LogInformation("Device registered successfully. Server message: {Message}", result.Message);
                return true;
            }

            _logger.LogError("EMS server rejected the registration: {Message}", result?.Message ?? "(no message)");
            return false;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (IsTransient(response.StatusCode))
        {
            _logger.LogWarning(
                "Attempt {Attempt}/{MaxAttempts}: server returned {StatusCode}. Response: {Body}",
                attempt, maxAttempts, (int)response.StatusCode, body);
            return null;
        }

        _logger.LogError(
            "Device registration failed with {StatusCode}. Response: {Body}",
            (int)response.StatusCode, body);
        return false;
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            || (int)statusCode >= 500;
    }

    /// <summary>Shape of the EMS.API registration response.</summary>
    private sealed record RegistrationResult(bool Success, string? Message, string? DeviceId, string? Token);

    /// <summary>Shape of the EMS.API heartbeat response.</summary>
    private sealed record HeartbeatResult(
        bool Success, string? Message, DateTime? ServerTime, bool UsbBlockingEnabled,
        IReadOnlyList<string>? BlockedWebsites);
}
