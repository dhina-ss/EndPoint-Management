using EMS.API.DTOs;

namespace EMS.API.Services;

public interface IUserService
{
    Task<CreateUserResult> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
}

public sealed record CreateUserResult(
    CreateUserOutcome Outcome, UserResponse? User = null, string? Error = null);

public enum CreateUserOutcome
{
    Created,
    DuplicateEmail,
    DuplicateUsername,
    DuplicateEmployeeCode
}
