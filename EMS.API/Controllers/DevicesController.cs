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

    public DevicesController(IDeviceService deviceService, IAppUsageService appUsageService)
    {
        _deviceService = deviceService;
        _appUsageService = appUsageService;
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
}
