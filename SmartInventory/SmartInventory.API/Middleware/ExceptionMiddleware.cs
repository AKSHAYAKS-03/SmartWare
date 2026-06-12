using System.Text.Json;
using SmartInventory.Core.Exceptions;

namespace SmartInventory.API.Middleware;


public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, errorCode, message, errors) = exception switch
        {
            InputValidationException valEx => (
                400,
                valEx.ErrorCode,
                valEx.Message,
                valEx.Errors
            ),
            SmartInventoryException domainEx => (
                domainEx.StatusCode,
                domainEx.ErrorCode,
                domainEx.Message,
                null
            ),
            Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => (
                409,
                "STALE_DATA",
                "The database record you attempted to modify was updated by another concurrent request. Please refresh and try again.",
                null
            ),
            _ => (
                500,
                "INTERNAL_SERVER_ERROR",
                "An unexpected internal server error occurred. Please contact support.",
                null
            )
        };

        context.Response.StatusCode = statusCode;

        var responsePayload = new
        {
            status = statusCode,
            code = errorCode,
            message = message,
            errors = errors,
            currentQuantity = (exception as StaleDataException)?.CurrentQuantity,
            timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(responsePayload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        await context.Response.WriteAsync(json);
    }
}
