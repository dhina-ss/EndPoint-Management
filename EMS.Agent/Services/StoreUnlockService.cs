using System.Net.Http.Json;
using EMS.Agent.Configuration;
using Microsoft.Extensions.Options;

namespace EMS.Agent.Services;

public class StoreUnlockService : IStoreUnlockService
{
    private readonly HttpClient _httpClient;
    private readonly ApiSettings _settings;
    private readonly IStoreUnlockStore _unlockStore;
    private readonly ILogger<StoreUnlockService> _logger;

    public StoreUnlockService(
        HttpClient httpClient,
        IOptions<ApiSettings> settings,
        IStoreUnlockStore unlockStore,
        ILogger<StoreUnlockService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _unlockStore = unlockStore;
        _logger = logger;
    }

    public async Task<StoreUnlockResult> UnlockAsync(
        string employeeCode, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeCode) || string.IsNullOrWhiteSpace(password))
        {
            return new StoreUnlockResult(false, "Enter the admin employee code and password.");
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                _settings.LoginEndpoint,
                new { employeeCode = employeeCode.Trim(), password },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Store unlock login failed with status {StatusCode}.", (int)response.StatusCode);
                return new StoreUnlockResult(false, $"Server error ({(int)response.StatusCode}). Try again.");
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResult>(cancellationToken);
            if (result?.Success != true)
            {
                return new StoreUnlockResult(false, result?.Message ?? "Invalid employee code or password.");
            }

            var duration = TimeSpan.FromMinutes(Math.Max(1, _settings.StoreUnlockMinutes));
            _unlockStore.GrantUnlock(duration);

            return new StoreUnlockResult(
                true, $"Microsoft Store unlocked for {duration.TotalMinutes:0} minutes.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not reach the EMS server at {BaseUrl}.", _httpClient.BaseAddress);
            return new StoreUnlockResult(false, "Could not reach the EMS server. Check the network and try again.");
        }
        catch (TaskCanceledException)
        {
            return new StoreUnlockResult(false, "The request timed out. Try again.");
        }
    }

    private sealed record LoginResult(bool Success, string? Message, string? Username, string? Email);
}
