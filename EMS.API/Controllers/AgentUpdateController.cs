using EMS.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace EMS.API.Controllers;

[ApiController]
[Route("api/agent/update")]
[Produces("application/json")]
public class AgentUpdateController : ControllerBase
{
    private readonly IAgentUpdateService _updateService;
    private readonly ILogger<AgentUpdateController> _logger;

    public AgentUpdateController(
        IAgentUpdateService updateService,
        ILogger<AgentUpdateController> logger)
    {
        _updateService = updateService;
        _logger = logger;
    }

    /// <summary>
    /// Checks available agent update status and metadata.
    /// </summary>
    [HttpGet("check")]
    [ProducesResponseType(typeof(AgentUpdateInfo), StatusCodes.Status200OK)]
    public ActionResult<AgentUpdateInfo> Check()
    {
        return Ok(_updateService.GetUpdateInfo());
    }

    /// <summary>
    /// Streams the agent installer binary for auto-updates.
    /// </summary>
    [HttpGet("download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(CancellationToken cancellationToken)
    {
        var stream = await _updateService.GetInstallerStreamAsync(cancellationToken);
        if (stream is null)
        {
            return NotFound(new { message = "Agent update installer is not available on the server." });
        }

        var fileName = _updateService.GetInstallerFileName();
        return File(stream, "application/octet-stream", fileName, enableRangeProcessing: true);
    }

    /// <summary>
    /// Uploads and registers a new agent update installer package.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(300L * 1024 * 1024)] // 300MB max
    [RequestFormLimits(MultipartBodyLengthLimit = 300L * 1024 * 1024)]
    [ProducesResponseType(typeof(AgentUpdateInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AgentUpdateInfo>> Upload(
        [FromForm] IFormFile file,
        [FromForm] string version,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "A non-empty installer file is required." });
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            return BadRequest(new { message = "A valid version string (e.g. 1.0.28) is required." });
        }

        await using var stream = file.OpenReadStream();
        var updateInfo = await _updateService.RegisterUpdatePackageAsync(version.Trim(), stream, cancellationToken);
        return Ok(updateInfo);
    }
}
