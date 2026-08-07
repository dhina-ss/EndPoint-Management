using System.Net;
using System.Net.Http.Json;

namespace EMS.API.Services;

/// <summary>An approximate location resolved from an IP address.</summary>
public sealed record GeoLocation(
    string? City, string? Region, string? Country, double Latitude, double Longitude);

public interface IGeoLocationService
{
    /// <summary>
    /// Resolves an IPv4/IPv6 address to an approximate city-level location, or
    /// null when the address is private/unroutable or the lookup fails.
    /// </summary>
    Task<GeoLocation?> ResolveAsync(string? ipAddress, CancellationToken cancellationToken = default);
}

/// <summary>
/// City-level geolocation via the free ip-api.com service. Called only when a
/// device's public IP changes, so it stays well within the free rate limit.
/// </summary>
public class GeoLocationService : IGeoLocationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeoLocationService> _logger;

    public GeoLocationService(HttpClient httpClient, ILogger<GeoLocationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>True for a public, routable address worth geolocating.</summary>
    public static bool IsPublicRoutable(string? ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
        {
            return false;
        }

        if (IPAddress.IsLoopback(ip))
        {
            return false;
        }

        var bytes = ip.GetAddressBytes();
        if (bytes.Length == 4)
        {
            // RFC1918 private ranges + link-local (169.254/16) + CGNAT (100.64/10).
            if (bytes[0] == 10) return false;
            if (bytes[0] == 192 && bytes[1] == 168) return false;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return false;
            if (bytes[0] == 169 && bytes[1] == 254) return false;
            if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) return false;
        }
        else
        {
            // IPv6 unique-local (fc00::/7) and link-local (fe80::/10).
            if ((bytes[0] & 0xFE) == 0xFC) return false;
            if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80) return false;
        }

        return true;
    }

    public async Task<GeoLocation?> ResolveAsync(string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (!IsPublicRoutable(ipAddress))
        {
            return null;
        }

        try
        {
            // Free endpoint (HTTP only): server-to-server, no key required.
            var url = $"http://ip-api.com/json/{ipAddress}?fields=status,message,country,regionName,city,lat,lon";
            var result = await _httpClient.GetFromJsonAsync<IpApiResult>(url, cancellationToken);

            if (result is null || !string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Geolocation lookup for {Ip} did not succeed: {Message}.", ipAddress, result?.Message);
                return null;
            }

            return new GeoLocation(result.City, result.RegionName, result.Country, result.Lat, result.Lon);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Geolocation lookup failed for {Ip}.", ipAddress);
            return null;
        }
    }

    private sealed record IpApiResult(
        string? Status, string? Message, string? Country, string? RegionName, string? City, double Lat, double Lon);
}
