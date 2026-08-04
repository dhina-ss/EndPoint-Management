using EMS.API.DTOs;
using EMS.API.Entities;
using EMS.API.Repositories;
using Microsoft.AspNetCore.Identity;

namespace EMS.API.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository repository,
        IPasswordHasher<AppUser> passwordHasher,
        ILogger<UserService> logger)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<CreateUserResult> CreateAsync(
        CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim();
        var employeeCode = request.EmployeeCode.Trim();
        var username = request.Username.Trim();

        // Check each unique field so the caller gets a precise message rather
        // than a generic 500 from the database's unique constraint.
        if (await _repository.EmailExistsAsync(email, cancellationToken))
        {
            return new CreateUserResult(CreateUserOutcome.DuplicateEmail, Error: "That email is already registered.");
        }

        if (await _repository.UsernameExistsAsync(username, cancellationToken))
        {
            return new CreateUserResult(CreateUserOutcome.DuplicateUsername, Error: "That username is already taken.");
        }

        if (await _repository.EmployeeCodeExistsAsync(employeeCode, cancellationToken))
        {
            return new CreateUserResult(
                CreateUserOutcome.DuplicateEmployeeCode, Error: "That employee code is already registered.");
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            EmployeeCode = employeeCode,
            Username = username,
            CreatedDate = DateTime.UtcNow
        };

        // Salted PBKDF2 via the framework hasher; the plain password is never
        // stored or logged.
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await _repository.AddAsync(user, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Dashboard user {Username} created ({Email}).", username, email);

        return new CreateUserResult(CreateUserOutcome.Created, ToResponse(user));
    }

    public async Task<IReadOnlyList<UserResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await _repository.GetAllAsync(cancellationToken);
        return users.Select(ToResponse).ToList();
    }

    private static UserResponse ToResponse(AppUser user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        EmployeeCode = user.EmployeeCode,
        Username = user.Username,
        CreatedDate = user.CreatedDate
    };
}
