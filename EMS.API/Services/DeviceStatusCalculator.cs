namespace EMS.API.Services;

/// <summary>
/// Single source of truth for a device's status so the device list, details
/// and metrics endpoints never disagree.
/// </summary>
public static class DeviceStatusCalculator
{
    /// <summary>
    /// Online while heartbeats are recent (within three missed beats). Sleep
    /// when a suspend beacon arrived after the last heartbeat and the device is
    /// no longer beating (best-effort). Offline otherwise.
    /// </summary>
    public static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(3);

    // A very old suspend is treated as Offline (the machine likely shut down
    // during sleep, or the resume beacon was missed).
    private static readonly TimeSpan MaxSleepAge = TimeSpan.FromHours(24);

    public const string Online = "Online";
    public const string Sleep = "Sleep";
    public const string Offline = "Offline";

    public static bool IsOnline(DateTime? lastHeartbeatTime, DateTime utcNow)
        => lastHeartbeatTime is not null && utcNow - lastHeartbeatTime.Value < OnlineThreshold;

    public static string Compute(DateTime? lastHeartbeatTime, DateTime? suspendedAt, DateTime utcNow)
    {
        if (IsOnline(lastHeartbeatTime, utcNow))
        {
            return Online;
        }

        var suspendedAfterLastBeat = suspendedAt is not null
            && (lastHeartbeatTime is null || suspendedAt.Value >= lastHeartbeatTime.Value)
            && utcNow - suspendedAt.Value < MaxSleepAge;

        return suspendedAfterLastBeat ? Sleep : Offline;
    }
}
