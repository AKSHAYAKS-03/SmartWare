namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Resolves the current authenticated user's identity and authorization context
/// from the JWT claims in the active HTTP request.
///
/// Injected into:
///   — AppDbContext (for audit trail UserId)
///   — Service layer (for role-based data scoping)
///   — AuditLog engine (for IP address capture)
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// The authenticated user's ID from the "userId" JWT claim.
    /// Falls back to the seeded admin GUID in background/test contexts.
    /// </summary>
    Guid UserId { get; }

    /// <summary>
    /// The role name from the "role" JWT claim (Admin / Manager / Staff / Viewer).
    /// Used to make authorization decisions in the service layer.
    /// </summary>
    string? Role { get; }

    /// <summary>
    /// The primary warehouse ID assigned to this user from the "warehouseId" JWT claim.
    /// Null for Admin and Viewer roles (unscoped — see all warehouses).
    /// </summary>
    Guid? WarehouseId { get; }

    /// <summary>
    /// The client IP address — captured for audit log entries.
    /// </summary>
    string? IpAddress { get; }

    /// <summary>
    /// Checks whether the current user has one of the specified roles.
    /// </summary>
    bool IsInRole(params string[] roles);

    /// <summary>
    /// Gets the current ClaimsPrincipal for use with IAuthorizationService.
    /// </summary>
    System.Security.Claims.ClaimsPrincipal? Principal { get; }
}
