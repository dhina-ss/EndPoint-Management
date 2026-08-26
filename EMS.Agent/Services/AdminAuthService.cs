using System.Net.Http.Json;
using EMS.Agent.Configuration;
using Microsoft.Extensions.Options;

namespace EMS.Agent.Services;

public sealed record AdminAuthResult(bool Success, string Message);

/// <summary>
/// Verifies EMS administrator credentials against the server, with no side
/// effects (unlike activation or Store unlock). Used to authorize sensitive
/// local actions such as uninstalling the agent.
/// </summary>
public interface IAdminAuthService
{
    Task<AdminAuthResult> VerifyAsync(
        string email, string password, CancellationToken cancellationToken = default);
}

public class AdminAuthService : IAdminAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ApiSettings _settings;
    private readonly ILogger<AdminAuthService> _logger;

    public AdminAuthService(
        HttpClient httpClient, IOptions<ApiSettings> settings, ILogger<AdminAuthService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<AdminAuthResult> VerifyAsync(
        string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return new AdminAuthResult(false, "Enter the admin email and password.");
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                _settings.LoginEndpoint,
                new { email = email.Trim(), password },
                cancellationToken);

            // Wrong credentials come back as 401; treat that as "invalid",
            // not a transient server error.
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new AdminAuthResult(false, "Invalid admin email or password.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Uninstall auth login failed with status {StatusCode}.", (int)response.StatusCode);
                return new AdminAuthResult(false, $"Server error ({(int)response.StatusCode}). Try again.");
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResult>(cancellationToken);
            return result?.Success == true
                ? new AdminAuthResult(true, "Authorized.")
                : new AdminAuthResult(false, result?.Message ?? "Invalid admin email or password.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not reach the EMS server at {BaseUrl}.", _httpClient.BaseAddress);
            return new AdminAuthResult(false, "Could not reach the EMS server. A connection is required to uninstall.");
        }
        catch (TaskCanceledException)
        {
            return new AdminAuthResult(false, "The request timed out. Try again.");
        }
    }

    private sealed record LoginResult(bool Success, string? Message, string? Username, string? Email);
}
