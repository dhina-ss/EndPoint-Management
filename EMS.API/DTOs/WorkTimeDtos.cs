namespace EMS.API.DTOs;

/// <summary>One day's working-time delta reported by an agent.</summary>
public class WorkTimeDelta
{
    public DateOnly WorkDate { get; set; }

    public int SecondsDelta { get; set; }
}

/// <summary>A batch of working-time deltas.</summary>
public class WorkTimeReportRequest
{
    public List<WorkTimeDelta> Sessions { get; set; } = new();
}

/// <summary>One day's cumulative working time, for the dashboard.</summary>
public class WorkTimeResponse
{
    public DateOnly WorkDate { get; set; }

    public int WorkedSeconds { get; set; }
}

/// <summary>Agent beacon: whether the device is entering (or leaving) sleep.</summary>
public class PowerStateRequest
{
    public bool Suspended { get; set; }
}
