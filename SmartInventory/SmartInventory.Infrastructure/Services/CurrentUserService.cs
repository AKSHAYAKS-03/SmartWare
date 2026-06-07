using SmartInventory.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;

namespace SmartInventory.Infrastructure.Services;

/// <summary>
/// Reads the current authenticated user's claims directly from the ASP.NET HTTP context.
///
/// This service is the bridge between the JWT token and the service layer.
/// Every service method that needs to scope data (e.g. Manager sees only their warehouse)
/// injects ICurrentUserService and reads the claims — zero extra DB lookups per request.
///
/// Claims extracted:
///   - userId  → "sub" or "userId" claim
///   - role    → "role" claim (Admin/Manager/Staff/Viewer)
///   - warehouseId → "warehouseId" claim (set at login; null for Admin/Viewer)
///   - IpAddress → from RemoteIpAddress on the connection
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// The authenticated user's ID, parsed from the "userId" JWT claim.
    /// Falls back to the seeded admin GUID for background / test contexts (no HTTP request).
    /// </summary>
    public Guid UserId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User.FindFirst("userId")?.Value
                     ?? _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;

            return Guid.TryParse(claim, out var id)
                ? id
                : Guid.Empty; // FAIL SECURELY: Prevents unauthenticated users from gaining Admin privileges
        }
    }

    /// <summary>
    /// The role name from the "role" JWT claim. Used for authorization decisions in the service layer.
    /// </summary>
    public string? Role =>
        _httpContextAccessor.HttpContext?.User.FindFirst("role")?.Value;

    /// <summary>
    /// The primary warehouse ID for this user from the "warehouseId" JWT claim.
    /// Null for Admin and Viewer roles (they are unscoped — see all warehouses).
    /// Manager and Staff always have a warehouseId here.
    /// </summary>
    public Guid? WarehouseId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User.FindFirst("warehouseId")?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    /// <summary>
    /// The client IP address — captured for audit log entries.
    /// </summary>
    public string? IpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    /// <summary>
    /// Checks whether the current user has one of the specified roles.
    /// </summary>
    public bool IsInRole(params string[] roles) =>
        roles.Contains(Role, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the current ClaimsPrincipal for use with IAuthorizationService.
    /// </summary>
    public System.Security.Claims.ClaimsPrincipal? Principal =>
        _httpContextAccessor.HttpContext?.User;
}
