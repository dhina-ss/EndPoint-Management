namespace EMS.Agent.Models;

/// <summary>
/// Wire shape for POST /api/devices/app-usage: mirrors EMS.API's AppUsageReportRequest.
/// </summary>
public class AppUsageReportPayload
{
    public List<AppUsageModel> UsageRecords { get; set; } = new();
}
