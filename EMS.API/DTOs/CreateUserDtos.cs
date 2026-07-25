using System.ComponentModel.DataAnnotations;

namespace EMS.API.DTOs;

/// <summary>
/// Request to create a dashboard user. ConfirmPassword is validated for a
/// match server-side too, not only in the browser.
/// </summary>
public class CreateUserRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string EmployeeCode { get; set; } = string.Empty;

    [Required]
    [MinLength(3)]
    [MaxLength(50)]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$",
        ErrorMessage = "Username may contain only letters, numbers, and . _ - characters.")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password), ErrorMessage = "The password and confirmation do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>The created user, never including any password material.</summary>
public class UserResponse
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string EmployeeCode { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }
}
