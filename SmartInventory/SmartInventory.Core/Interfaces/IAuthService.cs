using SmartInventory.Core.DTOs;

namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Defines the full authentication and user-session contract.
/// Supports JWT access tokens with refresh-token rotation and secure password management.
/// Internal employee accounts are provisioned by Admin via IUserService.CreateUserAsync.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Authenticates an internal user (Admin/Manager/Staff/Viewer).
    /// Issues a JWT access token and a rotated refresh token.
    /// Embeds userId, role, and assignedWarehouseId into the JWT payload.
    /// </summary>
    Task<LoginResponseDto?> SignInAsync(LoginDto dto);

    /// <summary>
    /// Validates an existing refresh token and issues a new access + refresh token pair (rotation).
    /// Automatically revokes the consumed refresh token.
    /// </summary>
    Task<LoginResponseDto?> RefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Revokes a refresh token (user logout). Marks the token IsRevoked = true in the database.
    /// </summary>
    Task RevokeTokenAsync(string refreshToken);

    /// <summary>
    /// Verifies the current password and replaces it with a BCrypt-hashed new password.
    /// </summary>
    Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto);

    /// <summary>
    /// Validates a one-time invite token and sets the employee's own password.
    /// Activates the user account (Status → Active) on success.
    /// The token is invalidated after use — cannot be reused.
    /// </summary>
    Task SetPasswordAsync(SetPasswordDto dto);
}
