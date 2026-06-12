using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.Enums;
using SmartInventory.Repository;

namespace SmartInventory.API.Middleware;

/// Security middleware that blocks requests carrying the "Supplier" role
/// from accessing any internal API route that does NOT start with /api/supplier/.
///
/// Also enforces onboarding state rules: non-Active suppliers cannot access
/// transactional features (POs, Invoices, Catalogue, Dashboard).
public class SupplierAuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SupplierAuthorizationMiddleware> _logger;

    public SupplierAuthorizationMiddleware(RequestDelegate next, ILogger<SupplierAuthorizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        // Only evaluate authenticated requests
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var role = context.User.FindFirstValue(ClaimTypes.Role)
                       ?? context.User.FindFirstValue("role");

            if (role == "Supplier")
            {
                var path = context.Request.Path.Value ?? string.Empty;

                // Supplier tokens are ONLY allowed on /api/supplier/* or /api/v{version}/supplier/* paths
                bool isSupplierPath = path.StartsWith("/api/supplier", StringComparison.OrdinalIgnoreCase)
                                   || path.StartsWith("/api/v1/supplier", StringComparison.OrdinalIgnoreCase);

                if (!isSupplierPath)
                {
                    _logger.LogWarning(
                        "SUPPLIER BLOCK: Contact {ContactId} attempted to access restricted path {Path}.",
                        context.User.FindFirstValue("contactId"),
                        path);

                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        type = "https://httpstatuses.com/403",
                        title = "Access Denied",
                        status = 403,
                        detail = "Supplier portal accounts cannot access internal system endpoints."
                    });
                    return;
                }

                // Block non-Active suppliers from accessing transactional features (anything under /api/supplier/ except auth and profile)
                bool isAuthOrProfile = path.StartsWith("/api/supplier/auth", StringComparison.OrdinalIgnoreCase)
                                    || path.StartsWith("/api/supplier/profile", StringComparison.OrdinalIgnoreCase)
                                    || path.StartsWith("/api/v1/supplier/auth", StringComparison.OrdinalIgnoreCase)
                                    || path.StartsWith("/api/v1/supplier/profile", StringComparison.OrdinalIgnoreCase);

                if (!isAuthOrProfile)
                {
                    var supplierIdStr = context.User.FindFirstValue("supplierId");
                    if (Guid.TryParse(supplierIdStr, out var supplierId))
                    {
                        var supplier = await db.Suppliers
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(s => s.Id == supplierId);

                        if (supplier == null || supplier.Status != SupplierStatus.Active)
                        {
                            _logger.LogWarning(
                                "SUPPLIER PORTAL LOCKED: Supplier {SupplierId} (Status: {Status}) attempted to access {Path}.",
                                supplierId,
                                supplier?.Status.ToString() ?? "NotFound",
                                path);

                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            await context.Response.WriteAsJsonAsync(new
                            {
                                type = "https://httpstatuses.com/403",
                                title = "Onboarding Incomplete",
                                status = 403,
                                detail = $"Your supplier portal account is not fully active. Current status: {supplier?.Status.ToString() ?? "Unknown"}.",
                                supplierStatus = supplier?.Status.ToString() ?? "Unknown"
                            });
                            return;
                        }
                    }
                }
            }
        }

        await _next(context);
    }
}
