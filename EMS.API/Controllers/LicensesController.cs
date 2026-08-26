using EMS.API.DTOs;
using EMS.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace EMS.API.Controllers;

[ApiController]
[Route("api/licenses")]
[Produces("application/json")]
public class LicensesController : ControllerBase
{
    private readonly ILicenseService _licenseService;

    public LicensesController(ILicenseService licenseService)
    {
        _licenseService = licenseService;
    }

    /// <summary>
    /// Step 1 of agent activation: validates a tenant license key. Anonymous by
    /// necessity — it runs before the device is registered, so it cannot require
    /// a device token. Always returns 200; check <c>valid</c> in the body.
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(LicenseValidateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LicenseValidateResponse>> Validate(
        [FromBody] LicenseValidateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _licenseService.ValidateAsync(request.LicenseKey, cancellationToken);
        return Ok(result);
    }
}
