using System.Runtime.Versioning;
using Windows.Devices.Geolocation;

namespace EMS.Agent.Helpers;

/// <summary>
/// Reads the device's precise location from Windows Location Services (GPS,
/// or Wi-Fi/cell positioning where there is no GPS radio). Entirely
/// best-effort: if Location Services are off, access is denied, there is no
/// hardware, or the API throws, it returns null and the caller falls back to
/// the server's IP-based location. Must run in the interactive user session
/// (the SYSTEM service cannot use the Location API).
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public static class GpsLocationHelper
{
    public static async Task<GpsReading?> TryReadAsync(ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            var access = await Geolocator.RequestAccessAsync();
            if (access != GeolocationAccessStatus.Allowed)
            {
                logger.LogInformation("Windows location access is {Status}; using IP location instead.", access);
                return null;
            }

            var locator = new Geolocator { DesiredAccuracyInMeters = 50 };

            // Accept a position up to 5 min old; wait up to 20s for a fresh fix.
            var position = await locator.GetGeopositionAsync(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(20));
            var point = position.Coordinate.Point.Position;

            logger.LogInformation(
                "GPS location acquired: {Lat}, {Lon} (±{Accuracy}m).",
                point.Latitude, point.Longitude, position.Coordinate.Accuracy);

            return new GpsReading(point.Latitude, point.Longitude, position.Coordinate.Accuracy);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "GPS location read failed; falling back to IP location.");
            return null;
        }
    }
}

/// <summary>A precise location fix from Windows Location Services.</summary>
public sealed record GpsReading(double Latitude, double Longitude, double AccuracyMeters);
