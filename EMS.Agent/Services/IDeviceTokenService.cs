namespace EMS.Agent.Services;

/// <summary>
/// Stores the device API token in device-auth.json so it survives service
/// restarts: heartbeats can authenticate immediately on startup instead of
/// waiting for the first registration to issue a fresh token.
/// </summary>
public interface IDeviceTokenService
{
    /// <summary>
    /// The current token — from cache or disk — or null when the agent has
    /// never registered (or the stored credential belongs to another identity).
    /// </summary>
    Task<string?> GetTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves a newly issued token to cache and disk.</summary>
    Task SaveTokenAsync(string deviceId, string token, CancellationToken cancellationToken = default);
}
