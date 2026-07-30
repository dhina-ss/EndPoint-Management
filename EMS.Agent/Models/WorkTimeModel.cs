namespace EMS.Agent.Models;

/// <summary>
/// Working-time accrued for one device-local calendar day since the last
/// upload. The server increments the day's running total by this delta.
/// </summary>
public class WorkTimeModel
{
    public DateOnly WorkDate { get; set; }

    public int SecondsDelta { get; set; }
}

/// <summary>Upload envelope for a batch of work-time deltas.</summary>
public class WorkTimeReportPayload
{
    public List<WorkTimeModel> Sessions { get; set; } = new();
}
