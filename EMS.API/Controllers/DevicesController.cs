using EMS.API.DTOs;
using EMS.API.Middleware;
using EMS.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace EMS.API.Controllers;

[ApiController]
[Route("api/devices")]
[Produces("application/json")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly IAppUsageService _appUsageService;
    private readonly IBlockedWebsiteService _blockedWebsiteService;
    private readonly IHeartbeatService _heartbeatService;
    private readonly IApplicationInventoryService _applicationService;

    public DevicesController(
        IDeviceService deviceService,
        IAppUsageService appUsageService,
        IBlockedWebsiteService blockedWebsiteService,
        IHeartbeatService heartbeatService,
        IApplicationInventoryService applicationService)
    {
        _deviceService = deviceService;
        _appUsageService = appUsageService;
        _blockedWebsiteService = blockedWebsiteService;
        _heartbeatService = heartbeatService;
        _applicationService = applicationService;
    }

    /// <summary>
    /// Registers a new device or refreshes the inventory of an existing one.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(DeviceRegisterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DeviceRegisterResponse>> Register(
        [FromBody] DeviceRegisterRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _deviceService.RegisterAsync(request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Returns all registered devices. Requires device credentials until a
    /// dedicated admin authentication scheme exists.
    /// </summary>
    [HttpGet]
    [RequireDeviceAuth]
    [ProducesResponseType(typeof(IReadOnlyList<DeviceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DeviceAuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<DeviceResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var devices = await _deviceService.GetAllAsync(cancellationToken);
        return Ok(devices);
    }

    /// <summary>
    /// Returns a single device by its internal id.
    /// </summary>
    [HttpGet("{id:guid}")]
    [RequireDeviceAuth]
    [ProducesResponseType(typeof(DeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(DeviceAuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DeviceResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var device = await _deviceService.GetByIdAsync(id, cancellationToken);
        return device is null ? NotFound() : Ok(device);
    }

    /// <summary>
    /// Enables or disables USB mass-storage blocking for a device. Takes
    /// effect on the device's next heartbeat, not instantly.
    /// </summary>
    [HttpPut("{id:guid}/usb-blocking")]
    [RequireDeviceAuth]
    [ProducesResponseType(typeof(DeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(DeviceAuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DeviceResponse>> SetUsbBlocking(
        Guid id,
        [FromBody] UsbBlockingUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var device = await _deviceService.SetUsbBlockingAsync(id, request.Enabled, cancellationToken);
        return device is null ? NotFound() : Ok(device);
    }

    /// <summary>
    /// Enables or disables Microsoft Store gating for a device. When enabled,
    /// the agent keeps the Store disabled until a user unlocks it locally with
    /// an EMS admin password. Takes effect on the device's next heartbeat.
    /// </summary>
    [HttpPut("{id:guid}/store-gating")]
    [RequireDeviceAuth]
    [ProducesResponseType(typeof(DeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(DeviceAuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DeviceResponse>> SetStoreGating(
        Guid id,
        [FromBody] UsbBlockingUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var device = await _deviceService.SetStoreGatingAsync(id, request.Enabled, cancellationToken);
        return device is null ? NotFound() : Ok(device);
    }

    /// <summary>
    /// Returns per-application foreground-usage totals for a device on a
    /// given day (defaults to today, UTC).
    /// </summary>
    [HttpGet("{id:guid}/app-usage")]
    [RequireDeviceAuth]
    [ProducesResponseType(typeof(IReadOnlyList<AppUsageSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DeviceAuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<AppUsageSummaryResponse>>> GetAppUsage(
        Guid id,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var usageDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var usage = await _appUsageService.GetUsageAsync(id, usageDate, cancellationToken);
        return Ok(usage);
    }

    /// <summary>
    /// Receives a full installed-application scan from an agent, replacing
    /// whatever was previously recorded for that device.
    /// </summary>
    [HttpPost("installed-apps")]
    [RequireDeviceAuth]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(DeviceAuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReportInstalledApps(
        [FromBody] InstalledAppsReportRequest request,
        CancellationToken cancellationToken)
    {
        var deviceId = Request.Headers[DeviceAuthenticationMiddleware.DeviceIdHeader].ToString();
        var stored = await _applicationService.ReplaceInventoryAsync(deviceId, request, cancellationToken);
        return stored ? NoContent() : NotFound();
    }

    /// <summary>
    /// Installed applications on a device, each flagged with whether it is
    /// currently blocked.
    /// </summary>
    [HttpGet("{id:guid}/installed-apps")]
    [RequireDeviceAuth]
    [ProducesResponseType(typeof(IReadOnlyList<InstalledAppResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(DeviceAuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<InstalledAppResponse>>> GetInstalledApps(
        Guid id, CancellationToken cancellationToken)
    {
        var apps = await _applicationService.GetInventoryAsync(id, cancellationToken);
        return apps is null ? NotFound() : Ok(apps);
    }

    /// <summary>
    /// Latest live-monitoring snapshot (CPU, memory, disk, network, uptime,
    /// battery, online state) for a device.
    /// </summary>
    [HttpGet("{id:guid}/metrics")]
    [RequireDeviceAuth]
    [ProducesResponseType(typeof(DeviceMetricsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(DeviceAuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DeviceMetricsResponse>> GetMetrics(
        Guid id, CancellationToken cancellationToken)
    {
        var metrics = await _heartbeatService.GetLatestMetricsAsync(id, cancellationToken);
        return metrics is null ? NotFound() : Ok(metrics);
    }

    /// <summary>
    /// Lists the device-specific blocked domains (in addition to the agent's
    /// always-on default phishing/malware list).
    /// </summary>
    [HttpGet("{id:guid}/blocked-websites")]
    [RequireDeviceAuth]
    [ProducesResponseType(typeof(IReadOnlyList<BlockedWebsiteResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(DeviceAuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<BlockedWebsiteResponse>>> GetBlockedWebsites(
        Guid id, CancellationToken cancellationToken)
    {
        var blocks = await _blockedWebsiteService.GetForDeviceAsync(id, cancellationToken);
        return blocks is null ? NotFound() : Ok(blocks);
    }

    /// <summary>
    /// Adds a domain to the device's block list. Takes effect on the device's
    /// next heartbeat.
    /// </summary>
    [HttpPost("{id:guid}/blocked-websites")]
    [RequireDeviceAuth]
    [ProducesResponseType(typeof(BlockedWebsiteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(DeviceAuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BlockedWebsiteResponse>> AddBlockedWebsite(
        Guid id,
        [FromBody] AddBlockedWebsiteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _blockedWebsiteService.AddAsync(id, request.Domain, cancellationToken);

        return result.Outcome switch
        {
            AddBlockedWebsiteOutcome.Created => CreatedAtAction(
                nameof(GetBlockedWebsites), new { id }, result.Created),
            AddBlockedWebsiteOutcome.DeviceNotFound => NotFound(),
            AddBlockedWebsiteOutcome.InvalidDomain => BadRequest(new { message = result.Error }),
            AddBlockedWebsiteOutcome.Duplicate => Conflict(new { message = result.Error }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>Removes a domain from the device's block list.</summary>
    [HttpDelete("{id:guid}/blocked-websites/{blockId:guid}")]
    [RequireDeviceAuth]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(DeviceAuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveBlockedWebsite(
        Guid id, Guid blockId, CancellationToken cancellationToken)
    {
        var removed = await _blockedWebsiteService.RemoveAsync(id, blockId, cancellationToken);
        return removed ? NoContent() : NotFound();
    }
}
