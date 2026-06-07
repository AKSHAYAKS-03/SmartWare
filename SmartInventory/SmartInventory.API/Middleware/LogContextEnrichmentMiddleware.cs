using Microsoft.AspNetCore.Http;
using Serilog.Context;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartInventory.API.Middleware;

public class LogContextEnrichmentMiddleware
{
    private readonly RequestDelegate _next;

    public LogContextEnrichmentMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract UserId from the active ClaimsPrincipal
        var userId = context.User?.FindFirstValue("userId") ?? context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrEmpty(userId))
        {
            // Push UserId to Serilog LogContext so every log in this request scope includes it
            using (LogContext.PushProperty("UserId", userId))
            {
                await _next(context);
            }
        }
        else
        {
            await _next(context);
        }
    }
}
