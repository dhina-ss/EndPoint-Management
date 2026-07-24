namespace EMS.Agent.Models;

/// <summary>
/// Wire shape for the installed-application inventory report; mirrors the
/// EMS.API InstalledAppsReportRequest contract.
/// </summary>
public class InstalledAppsPayload
{
    public List<InstalledAppModel> Applications { get; set; } = new();
}
