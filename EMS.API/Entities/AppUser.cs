namespace EMS.API.Entities;

/// <summary>
/// A dashboard/admin user (a person who signs in to manage the fleet),
/// distinct from device credentials. The password is stored only as a
/// salted PBKDF2 hash, never in plain text.
/// </summary>
public class AppUser
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    /// <summary>Organization employee code.</summary>
    public string EmployeeCode { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    /// <summary>Salted PBKDF2 hash produced by the ASP.NET Core PasswordHasher.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }
}
