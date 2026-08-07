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

        var response = await _heartbeatService.RecordHeartbeatAsync(
            deviceId, request, ResolveClientIp(), cancellationToken);

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

    /// <summary>
    /// The device's public IP, for geolocation. Behind the platform proxy the
    /// connection's remote IP is an internal proxy address, so prefer the
    /// leftmost X-Forwarded-For entry (the original client as the edge saw it).
    /// </summary>
    private string? ResolveClientIp()
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var first = forwardedFor.Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(first))
            {
                // Strip an optional :port (IPv4) while leaving bracketed IPv6 intact.
                var colon = first.IndexOf(':');
                if (colon > 0 && first.IndexOf(':', colon + 1) < 0 && !first.Contains('['))
                {
                    first = first[..colon];
                }

                return first;
            }
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
