using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.API.Controllers;

/// Handles all authentication and user-session operations.

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUser;

    public AuthController(IAuthService authService, ICurrentUserService currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    
    /// Authenticates an internal user (Admin/Manager/Staff/Viewer) and returns a
    /// JWT access token + refresh token pair. Rate-limited to prevent brute-force attacks.
    /// New employee accounts must be created by an Admin via POST /api/v1/Users.
    
    [HttpPost("signin")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> SignIn([FromBody] LoginDto dto)
    {
        var result = await _authService.SignInAsync(dto);
        if (result == null) return Unauthorized(new { message = "Invalid email or password." });
        return Ok(result);
    }

    
    /// Exchanges a valid refresh token for a new access token + refresh token pair.
    /// The provided refresh token is immediately revoked after use (rotation).
    
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto)
    {
        var result = await _authService.RefreshTokenAsync(dto.Token);
        if (result == null) return Unauthorized(new { message = "Invalid, expired, or revoked refresh token." });
        return Ok(result);
    }

    
    /// Revokes the provided refresh token — effectively logs the user out.
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke([FromBody] RefreshTokenDto dto)
    {
        await _authService.RevokeTokenAsync(dto.Token);
        return NoContent();
    }

    
    /// Changes the authenticated user's password.
    /// All active refresh tokens are revoked on success — forces re-login on all devices.
    
    [HttpPut("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        await _authService.ChangePasswordAsync(_currentUser.UserId, dto);
        return NoContent();
    }

    
    /// Activates a newly provisioned employee account by setting their own password.
    /// Uses a one-time invite token sent to their email — no authentication required.
    /// The token is invalidated after use (cannot be replayed).
    /// Account Status transitions to Active on success.
    
    [HttpPost("set-password")]
    [AllowAnonymous]
    public async Task<IActionResult> SetPassword([FromBody] SetPasswordDto dto)
    {
        await _authService.SetPasswordAsync(dto);
        return Ok(new { message = "Password set successfully. You can now sign in to SmartWare." });
    }
}
