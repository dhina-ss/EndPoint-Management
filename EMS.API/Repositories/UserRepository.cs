using EMS.API.Data;
using EMS.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace EMS.API.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _dbContext;

    public UserRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        => _dbContext.AppUsers.AnyAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
        => _dbContext.AppUsers.AnyAsync(u => u.Username.ToLower() == username.ToLower(), cancellationToken);

    public Task<bool> EmployeeCodeExistsAsync(string employeeCode, CancellationToken cancellationToken = default)
        => _dbContext.AppUsers.AnyAsync(u => u.EmployeeCode.ToLower() == employeeCode.ToLower(), cancellationToken);

    public async Task AddAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        await _dbContext.AppUsers.AddAsync(user, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
