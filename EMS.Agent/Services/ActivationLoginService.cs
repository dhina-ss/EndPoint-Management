using System.Net.Http.Json;
using EMS.Agent.Configuration;
using Microsoft.Extensions.Options;

namespace EMS.Agent.Services;

public class ActivationLoginService : IActivationLoginService
{
    private readonly HttpClient _httpClient;
    private readonly ApiSettings _settings;
    private readonly IActivationStore _activationStore;
    private readonly ILogger<ActivationLoginService> _logger;

    public ActivationLoginService(
        HttpClient httpClient,
        IOptions<ApiSettings> settings,
        IActivationStore activationStore,
        ILogger<ActivationLoginService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _activationStore = activationStore;
        _logger = logger;
    }

    public async Task<ActivationLoginResult> LoginAndActivateAsync(
        string employeeCode, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeCode) || string.IsNullOrWhiteSpace(password))
        {
            return new ActivationLoginResult(false, "Enter your employee code and password.");
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                _settings.LoginEndpoint,
                new { employeeCode = employeeCode.Trim(), password },
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

            _activationStore.Activate(result.Username ?? employeeCode.Trim());
            return new ActivationLoginResult(true, "EMS activated successfully.");
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
