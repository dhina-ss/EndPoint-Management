using EMS.API.DTOs;
using EMS.API.Middleware;
using EMS.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace EMS.API.Controllers;

/// <summary>
/// Installer package library. Packages are uploaded once and then targeted at
/// devices via the device command queue (see <see cref="DevicesController"/>).
/// </summary>
[ApiController]
[Route("api/packages")]
[Produces("application/json")]
public class PackagesController : ControllerBase
{
    // Pilot cap: keeps the in-memory upload/download and the Postgres bytea
    // column to a sane size. Raise (and move to object storage) for production.
    private const long MaxUploadBytes = 200L * 1024 * 1024;

    private readonly IInstallerPackageService _packageService;

    public PackagesController(IInstallerPackageService packageService)
    {
        _packageService = packageService;
    }

    /// <summary>Uploads an MSI/EXE installer to the library.</summary>
    [HttpPost]
    [RequireDeviceAuth]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    [ProducesResponseType(typeof(InstallerPackageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(DeviceAuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<InstallerPackageResponse>> Upload(
        [FromForm] IFormFile file,
        [FromForm] string? displayName,
        [FromForm] string? silentArgs,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "A non-empty installer file is required." });
        }

        await using var stream = file.OpenReadStream();
        var result = await _packageService.UploadAsync(
            file.FileName, displayName, silentArgs, stream, cancellationToken);

        return result.Outcome switch
        {
            UploadPackageOutcome.Created => CreatedAtAction(nameof(GetAll), null, result.Package),
            _ => BadRequest(new { message = result.Error })
        };
    }

    /// <summary>Lists uploaded packages (metadata only).</summary>
    [HttpGet]
    [RequireDeviceAuth]
    [ProducesResponseType(typeof(IReadOnlyList<InstallerPackageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DeviceAuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<InstallerPackageResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        return Ok(await _packageService.GetAllAsync(cancellationToken));
    }

    /// <summary>Streams the installer bytes to the agent.</summary>
    [HttpGet("{id:guid}/content")]
    [RequireDeviceAuth]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(DeviceAuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetContent(Guid id, CancellationToken cancellationToken)
    {
        var content = await _packageService.GetContentAsync(id, cancellationToken);
        if (content is null)
        {
            return NotFound();
        }

        return File(content.Content, content.ContentType, content.FileName);
    }

    /// <summary>Deletes a package (blocked while a command still references it).</summary>
    [HttpDelete("{id:guid}")]
    [RequireDeviceAuth]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(DeviceAuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _packageService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
