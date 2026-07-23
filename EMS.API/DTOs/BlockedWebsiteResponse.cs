namespace EMS.API.DTOs;

/// <summary>
/// A device-specific blocked domain, as returned to the dashboard.
/// </summary>
public class BlockedWebsiteResponse
{
    public Guid Id { get; set; }

    public string Domain { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }
}
