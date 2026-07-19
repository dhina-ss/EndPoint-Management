using System.Text.Json;
using EMS.API.DTOs;
using EMS.API.Services;
using EMS.Shared.Constants;

namespace EMS.API.Middleware;

/// <summary>
/// Enforces device credentials on endpoints marked with
/// <see cref="RequireDeviceAuthAttribute"/>: reads X-Device-Id and
/// X-Device-Token headers and rejects the request with 401 when they are
/// missing or invalid. All other endpoints pass through untouched.
/// </summary>
public class DeviceAuthenticationMiddleware
{
    public const string DeviceIdHeader = DeviceAuthHeaders.DeviceId;
    public const string DeviceTokenHeader = DeviceAuthHeaders.Token;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<DeviceAuthenticationMiddleware> _logger;

    public DeviceAuthenticationMiddleware(RequestDelegate next, ILogger<DeviceAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<RequireDeviceAuthAttribute>() is null)
        {
            await _next(context);
            return;
        }

        var deviceId = context.Request.Headers[DeviceIdHeader].ToString();
        var token = context.Request.Headers[DeviceTokenHeader].ToString();

        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning(
                "Rejected request to {Path}: missing device credential headers.", context.Request.Path);
            await WriteUnauthorizedAsync(context, "Missing device credentials");
            return;
        }

        // The middleware is a singleton; the validator is scoped, so it must
        // be resolved from the request scope.
        var validator = context.RequestServices.GetRequiredService<ITokenValidationService>();

        if (!await validator.ValidateDeviceTokenAsync(deviceId, token, context.RequestAborted))
        {
            await WriteUnauthorizedAsync(context, "Invalid device credentials");
            return;
        }

        await _next(context);
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";

        var response = new DeviceAuthResponse
        {
            Success = false,
            Message = message
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, SerializerOptions));
    }
}
