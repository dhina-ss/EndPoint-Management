using System.Text.Json;
using EMS.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EMS.API.Middleware;

/// <summary>
/// Catches all unhandled exceptions and converts them into a uniform JSON error response.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected; nothing to report.
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteErrorResponseAsync(context, "Device registration failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteErrorResponseAsync(context, "An unexpected error occurred");
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var response = new DeviceRegisterResponse
        {
            Success = false,
            Message = message
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, SerializerOptions));
    }
}
