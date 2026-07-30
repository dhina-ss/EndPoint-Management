namespace EMS.API.DTOs;

/// <summary>One day's network data usage for a device.</summary>
public class NetworkUsageResponse
{
    public DateOnly UsageDate { get; set; }

    /// <summary>Total bytes uploaded on this day.</summary>
    public long BytesSent { get; set; }

    /// <summary>Total bytes downloaded on this day.</summary>
    public long BytesReceived { get; set; }
}
