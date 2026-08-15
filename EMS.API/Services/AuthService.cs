using EMS.API.DTOs;
using EMS.API.Entities;
using EMS.API.Repositories;
using Microsoft.AspNetCore.Identity;

namespace EMS.API.Services;

public class AuthService : IAuthService
{
    private const string InvalidMessage = "Invalid employee code or password.";

    private readonly IUserRepository _repository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository repository,
        IDeviceRepository deviceRepository,
        IPasswordHasher<AppUser> passwordHasher,
        ILogger<AuthService> logger)
    {
        _repository = repository;
        _deviceRepository = deviceRepository;
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

        // One account, one device: if the activating agent sent its DeviceId
        // and this user is already bound to a different device, refuse — the
        // account can't be used to activate a second endpoint.
        if (!string.IsNullOrWhiteSpace(request.DeviceId))
        {
            var boundDevice = await _deviceRepository.GetActivatedDeviceForUserAsync(user.Id, cancellationToken);
            if (boundDevice is not null &&
                !string.Equals(boundDevice.DeviceId, request.DeviceId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Activation blocked: user {Username} is already activated on device {BoundDevice}; " +
                    "rejected new device {NewDevice}.",
                    user.Username, boundDevice.DeviceId, request.DeviceId);

                return new LoginResponse
                {
                    Success = false,
                    Message =
                        $"This account is already activated on \"{boundDevice.DeviceName}\". " +
                        "One account can be used on only one device. " +
                        "Contact your administrator to release it first."
                };
            }
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
