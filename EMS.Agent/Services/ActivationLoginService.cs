using System.Net.Http.Json;
using EMS.Agent.Configuration;
using Microsoft.Extensions.Options;

namespace EMS.Agent.Services;

public class ActivationLoginService : IActivationLoginService
{
    private readonly HttpClient _httpClient;
    private readonly ApiSettings _settings;
    private readonly IActivationStore _activationStore;
    private readonly IDeviceIdService _deviceIdService;
    private readonly ILogger<ActivationLoginService> _logger;

    public ActivationLoginService(
        HttpClient httpClient,
        IOptions<ApiSettings> settings,
        IActivationStore activationStore,
        IDeviceIdService deviceIdService,
        ILogger<ActivationLoginService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _activationStore = activationStore;
        _deviceIdService = deviceIdService;
        _logger = logger;
    }

    public async Task<ActivationLoginResult> LoginAndActivateAsync(
        string employeeCode, string password, string licenseKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeCode) || string.IsNullOrWhiteSpace(password))
        {
            return new ActivationLoginResult(false, "Enter your employee code and password.");
        }

        try
        {
            // Send this endpoint's DeviceId so the server can enforce
            // one-account-per-device and reject activation on a second machine.
            // The licenseKey scopes the sign-in to its tenant: the server only
            // accepts the credentials if the user belongs to that tenant.
            var deviceId = await _deviceIdService.GetDeviceIdAsync(cancellationToken);

            using var response = await _httpClient.PostAsJsonAsync(
                _settings.LoginEndpoint,
                new { employeeCode = employeeCode.Trim(), password, deviceId, licenseKey = licenseKey.Trim() },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Login request failed with status {StatusCode}.", (int)response.StatusCode);
                return new ActivationLoginResult(false, $"Server error ({(int)response.StatusCode}). Try again.");
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResult>(cancellationToken);
            if (result?.Success != true)
            {
                return new ActivationLoginResult(false, result?.Message ?? "Invalid username or password.");
            }

            // Store the employee code and license key so the server can map this
            // device to that user + tenant on the next registration.
            _activationStore.Activate(employeeCode.Trim(), licenseKey.Trim());
            return new ActivationLoginResult(true, "The Lance Endpoint activated successfully.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not reach the EMS server at {BaseUrl}.", _httpClient.BaseAddress);
            return new ActivationLoginResult(false, "Could not reach the EMS server. Check the network and try again.");
        }
        catch (TaskCanceledException)
        {
            return new ActivationLoginResult(false, "The request timed out. Try again.");
        }
    }

    private sealed record LoginResult(bool Success, string? Message, string? Username, string? Email);
}
