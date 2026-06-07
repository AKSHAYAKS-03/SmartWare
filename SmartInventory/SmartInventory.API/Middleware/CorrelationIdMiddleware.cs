using Microsoft.AspNetCore.Http;
using Serilog.Context;
using System;
using System.Threading.Tasks;

namespace SmartInventory.API.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeaderKey = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Try to retrieve existing correlation ID from request headers
        if (!context.Request.Headers.TryGetValue(CorrelationIdHeaderKey, out var correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        // Add correlation ID to response headers
        context.Response.Headers[CorrelationIdHeaderKey] = correlationId;

        // Push correlation ID to Serilog LogContext
        using (LogContext.PushProperty("CorrelationId", correlationId.ToString()))
        {
            await _next(context);
        }
    }
}
