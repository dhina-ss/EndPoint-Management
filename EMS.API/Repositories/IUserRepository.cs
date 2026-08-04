using EMS.API.Entities;

namespace EMS.API.Repositories;

public interface IUserRepository
{
    /// <summary>Case-insensitive existence check across the three unique fields.</summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);

    Task<bool> EmployeeCodeExistsAsync(string employeeCode, CancellationToken cancellationToken = default);

    /// <summary>Finds a user by employee code, username, or email (case-insensitive) for sign-in.</summary>
    Task<AppUser?> GetByLoginIdentifierAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>All users, newest first.</summary>
    Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(AppUser user, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
