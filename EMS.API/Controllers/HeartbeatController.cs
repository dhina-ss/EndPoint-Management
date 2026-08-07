using EMS.API.DTOs;
using EMS.API.Middleware;
using EMS.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace EMS.API.Controllers;

[ApiController]
[Route("api/devices/heartbeat")]
[Produces("application/json")]
public class HeartbeatController : ControllerBase
{
    private readonly IHeartbeatService _heartbeatService;

    public HeartbeatController(IHeartbeatService heartbeatService)
    {
        _heartbeatService = heartbeatService;
    }

    /// <summary>
    /// Records a heartbeat for the authenticated device. Token validation is
    /// enforced by the device authentication middleware.
    /// </summary>
    [HttpPost]
    [RequireDeviceAuth]
    [ProducesResponseType(typeof(HeartbeatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DeviceAuthResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(HeartbeatResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HeartbeatResponse>> Heartbeat(
        [FromBody] HeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        var deviceId = Request.Headers[DeviceAuthenticationMiddleware.DeviceIdHeader].ToString();

        // The public IP the request arrived from (ForwardedHeaders resolves it
        // from X-Forwarded-For behind the platform proxy); used for geolocation.
        var publicIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var response = await _heartbeatService.RecordHeartbeatAsync(deviceId, request, publicIp, cancellationToken);

        if (response is null)
        {
            return NotFound(new HeartbeatResponse
            {
                Success = false,
                Message = "Device not found",
                ServerTime = DateTime.UtcNow
            });
        }

        return Ok(response);
    }
}
