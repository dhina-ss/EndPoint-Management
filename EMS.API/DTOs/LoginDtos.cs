using System.ComponentModel.DataAnnotations;

namespace EMS.API.DTOs;

/// <summary>Agent activation sign-in: an EMS user's credentials.</summary>
public class LoginRequest
{
    [Required]
    [MaxLength(256)]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Sign-in result. Never carries any password material.</summary>
public class LoginResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? Username { get; set; }

    public string? Email { get; set; }
}
