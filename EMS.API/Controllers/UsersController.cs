using EMS.API.DTOs;
using EMS.API.Middleware;
using EMS.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace EMS.API.Controllers;

[ApiController]
[Route("api/users")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Creates a dashboard user. Gated by [RequireDeviceAuth] so it is not a
    /// fully anonymous account-creation endpoint; a dedicated admin sign-in
    /// scheme is the intended longer-term replacement for that gate.
    /// </summary>
    [HttpPost]
    [RequireDeviceAuth]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(DeviceAuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _userService.CreateAsync(request, cancellationToken);

        return result.Outcome switch
        {
            CreateUserOutcome.Created => StatusCode(StatusCodes.Status201Created, result.User),
            _ => Conflict(new { message = result.Error })
        };
    }

    /// <summary>Lists dashboard users (newest first).</summary>
    [HttpGet]
    [RequireDeviceAuth]
    [ProducesResponseType(typeof(IReadOnlyList<UserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DeviceAuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _userService.GetAllAsync(cancellationToken));
    }
}
