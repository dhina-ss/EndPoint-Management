using EMS.API.DTOs;
using EMS.API.Middleware;
using EMS.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace EMS.API.Controllers;

[ApiController]
[Route("api/devices/app-usage")]
[Produces("application/json")]
public class AppUsageController : ControllerBase
{
    private readonly IAppUsageService _appUsageService;

    public AppUsageController(IAppUsageService appUsageService)
    {
        _appUsageService = appUsageService;
    }

    /// <summary>
    /// Records a batch of per-application foreground-usage deltas for the
    /// authenticated device. Token validation is enforced by the device
    /// authentication middleware.
    /// </summary>
    [HttpPost]
    [RequireDeviceAuth]
    [ProducesResponseType(typeof(AppUsageReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DeviceAuthResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppUsageReportResponse>> Report(
        [FromBody] AppUsageReportRequest request,
        CancellationToken cancellationToken)
    {
        var deviceId = Request.Headers[DeviceAuthenticationMiddleware.DeviceIdHeader].ToString();

        var response = await _appUsageService.RecordUsageAsync(deviceId, request, cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }
}
