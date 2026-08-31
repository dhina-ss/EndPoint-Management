using System.Net.Http.Json;
using EMS.Agent.Configuration;
using Microsoft.Extensions.Options;

namespace EMS.Agent.Services;

public sealed record LicenseValidationResult(
    bool Success, string Message, string? TenantId = null, string? TenantName = null);

/// <summary>
/// Step 1 of activation: validates a tenant license key against the server
/// before the user signs in. No side effects — it only confirms the key is
/// real, active, and not expired, and returns which tenant it belongs to.
/// </summary>
public interface ILicenseActivationService
{
    Task<LicenseValidationResult> ValidateAsync(
        string licenseKey, CancellationToken cancellationToken = default);
}

public class LicenseActivationService : ILicenseActivationService
{
    private readonly HttpClient _httpClient;
    private readonly ApiSettings _settings;
    private readonly ILoginPolicyStore _loginPolicyStore;
    private readonly ILogger<LicenseActivationService> _logger;

    public LicenseActivationService(
        HttpClient httpClient, IOptions<ApiSettings> settings,
        ILoginPolicyStore loginPolicyStore, ILogger<LicenseActivationService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _loginPolicyStore = loginPolicyStore;
        _logger = logger;
    }

    public async Task<LicenseValidationResult> ValidateAsync(
        string licenseKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return new LicenseValidationResult(false, "Enter your license key.");
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                _settings.LicenseValidateEndpoint,
                new { licenseKey = licenseKey.Trim() },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("License validation failed with status {StatusCode}.", (int)response.StatusCode);
                return new LicenseValidationResult(false, $"Server error ({(int)response.StatusCode}). Try again.");
            }

            var result = await response.Content.ReadFromJsonAsync<LicenseResult>(cancellationToken);
            if (result?.Valid != true)
            {
                return new LicenseValidationResult(false, result?.Message ?? "Invalid or expired license key.");
            }

            // Remember the tenant's "require sign-in each startup" policy so the
            // agent can enforce it even before the first activation. Preserve any
            // inactivity settings already learned from a heartbeat.
            _loginPolicyStore.Set(
                result.RequireLoginEachStartup,
                _loginPolicyStore.InactivityLockSeconds(),
                _loginPolicyStore.InactivityAlertSeconds());

            return new LicenseValidationResult(true, "License verified.", result.TenantId, result.TenantName);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not reach the server at {BaseUrl}.", _httpClient.BaseAddress);
            return new LicenseValidationResult(false, "Could not reach the server. Check the network and try again.");
        }
        catch (TaskCanceledException)
        {
            return new LicenseValidationResult(false, "The request timed out. Try again.");
        }
    }

    private sealed record LicenseResult(
        bool Valid, string? Message, string? TenantId, string? TenantName, bool RequireLoginEachStartup);
}
