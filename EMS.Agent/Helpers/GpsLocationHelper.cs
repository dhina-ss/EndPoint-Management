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
            var access = await RequestAccessSafeAsync(logger, cancellationToken);
            if (access != GeolocationAccessStatus.Allowed)
            {
                logger.LogWarning("Windows location access is {Status}; check Windows Privacy -> Location settings.", access);
                return null;
            }

            var locator = new Geolocator
            {
                DesiredAccuracy = PositionAccuracy.High,
                DesiredAccuracyInMeters = 100
            };

            // Accept a position up to 5 min old; try 100m accuracy first (12s),
            // then fall back to Default/500m (15s) for indoor Wi-Fi setups where
            // router triangulation has >100m uncertainty.
            Geoposition? position = null;
            try
            {
                position = await locator.GetGeopositionAsync(
                    TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(12)).AsTask(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogInformation("High-accuracy GPS read timed out/failed ({Msg}); retrying with Default Wi-Fi accuracy.", ex.Message);
                var fallbackLocator = new Geolocator
                {
                    DesiredAccuracy = PositionAccuracy.Default,
                    DesiredAccuracyInMeters = 500
                };
                position = await fallbackLocator.GetGeopositionAsync(
                    TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(15)).AsTask(cancellationToken);
            }

            if (position is null)
            {
                return null;
            }

            // If Windows only has an IP-derived location (no GPS/Wi-Fi/cell fix),
            // let the server handle IP geolocation so it isn't mislabeled as GPS.
            if (position.Coordinate.PositionSource == PositionSource.IPAddress)
            {
                logger.LogInformation(
                    "Windows location source is IPAddress (±{Accuracy}m); deferring to server IP location.",
                    position.Coordinate.Accuracy);
                return null;
            }

            var point = position.Coordinate.Point.Position;

            logger.LogInformation(
                "GPS location acquired ({Source}): {Lat}, {Lon} (±{Accuracy}m).",
                position.Coordinate.PositionSource, point.Latitude, point.Longitude, position.Coordinate.Accuracy);

            return new GpsReading(point.Latitude, point.Longitude, position.Coordinate.Accuracy);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GPS location read failed; falling back to IP location.");
            return null;
        }
    }

    private static async Task<GeolocationAccessStatus> RequestAccessSafeAsync(
        ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            var status = await Geolocator.RequestAccessAsync().AsTask(cancellationToken);
            if (status != GeolocationAccessStatus.Unspecified)
            {
                return status;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogInformation(ex, "Direct Geolocator.RequestAccessAsync failed; retrying on STA thread.");
        }

        // Some Windows 11 builds require an STA apartment for RequestAccessAsync().
        var tcs = new TaskCompletionSource<GeolocationAccessStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var status = Geolocator.RequestAccessAsync().AsTask(cancellationToken).GetAwaiter().GetResult();
                tcs.TrySetResult(status);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "EMS Location Access"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return await tcs.Task.WaitAsync(cancellationToken);
    }
}

/// <summary>A precise location fix from Windows Location Services.</summary>
public sealed record GpsReading(double Latitude, double Longitude, double AccuracyMeters);
