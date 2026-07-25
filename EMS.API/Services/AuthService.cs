using EMS.API.DTOs;
using EMS.API.Entities;
using EMS.API.Repositories;
using Microsoft.AspNetCore.Identity;

namespace EMS.API.Services;

public class AuthService : IAuthService
{
    private const string InvalidMessage = "Invalid employee code or password.";

    private readonly IUserRepository _repository;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository repository,
        IPasswordHasher<AppUser> passwordHasher,
        ILogger<AuthService> logger)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(
        LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetByLoginIdentifierAsync(request.EmployeeCode.Trim(), cancellationToken);
        if (user is null)
        {
            _logger.LogWarning("Login failed: no user matching {Identifier}.", request.EmployeeCode);
            return Failed();
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Login failed: wrong password for user {Username}.", user.Username);
            return Failed();
        }

        // Transparently upgrade the stored hash if the hasher's parameters
        // have since been strengthened.
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("User {Username} signed in.", user.Username);

        return new LoginResponse
        {
            Success = true,
            Message = "Login successful",
            Username = user.Username,
            Email = user.Email
        };
    }

    private static LoginResponse Failed() => new() { Success = false, Message = InvalidMessage };
}
