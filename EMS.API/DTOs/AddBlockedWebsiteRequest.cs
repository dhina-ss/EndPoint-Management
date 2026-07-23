using System.ComponentModel.DataAnnotations;

namespace EMS.API.DTOs;

/// <summary>
/// Request body for adding a domain to a device's block list. Accepts a bare
/// host, a full URL, or a "www." form; the service normalizes it to a host.
/// </summary>
public class AddBlockedWebsiteRequest
{
    [Required]
    [MaxLength(253)]
    public string Domain { get; set; } = string.Empty;
}
