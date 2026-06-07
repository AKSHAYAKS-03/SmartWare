using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Interfaces;
using System.Security.Claims;

namespace SmartInventory.API.Controllers;

/// <summary>
/// Supplier portal authentication endpoints.
/// Route prefix: /api/supplier/auth
///
/// POST /api/supplier/auth/login          — Supplier login (public)
/// POST /api/supplier/auth/refresh        — Token refresh (public)
/// POST /api/supplier/auth/logout         — Revoke refresh token [Supplier]
/// PUT  /api/supplier/auth/change-password — Change password [Supplier]
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/supplier/auth")]
public class SupplierAuthController : ControllerBase
{
    private readonly ISupplierAuthService _authService;

    public SupplierAuthController(ISupplierAuthService authService)
    {
        _authService = authService;
    }

    private string GetIpAddress() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>
    /// Authenticates a supplier contact with email + password.
    /// Returns a supplier-scoped JWT access token and refresh token.
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] SupplierLoginRequest request)
    {
        var result = await _authService.LoginAsync(request, GetIpAddress());
        return Ok(result);
    }

    /// <summary>
    /// Exchanges a valid supplier refresh token for a new token pair.
    /// The provided token is revoked immediately (rotation).
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] SupplierRefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request, GetIpAddress());
        return Ok(result);
    }

    /// <summary>
    /// Revokes the supplier's refresh token — logout.
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost("logout")]
    [Authorize(Policy = "RequireSupplier")]
    public async Task<IActionResult> Logout([FromBody] SupplierRefreshTokenRequest request)
    {
        await _authService.RevokeTokenAsync(request.RefreshToken, GetIpAddress());
        return NoContent();
    }

    /// <summary>
    /// Changes the authenticated supplier contact's password.
    /// All existing refresh tokens are revoked — forces re-login on all devices.
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPut("change-password")]
    [Authorize(Policy = "RequireSupplier")]
    public async Task<IActionResult> ChangePassword([FromBody] SupplierChangePasswordRequest request)
    {
        var contactId = Guid.Parse(User.FindFirstValue("contactId")!);
        await _authService.ChangePasswordAsync(contactId, request);
        return NoContent();
    }

    /// <summary>
    /// Handles supplier self-registration, creating a record in Registered state and generating an email verification OTP.
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] SupplierRegisterRequest request)
    {
        var contactId = await _authService.RegisterAsync(request);
        return Ok(new
        {
            message = "Registration successful. Please verify your email using the OTP token sent to you.",
            contactId
        });
    }

    /// <summary>
    /// Verifies the supplier contact's email using the OTP token, moving the supplier to PendingReview.
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromBody] SupplierVerifyEmailRequest request)
    {
        await _authService.VerifyEmailAsync(request);
        return Ok(new { message = "Email verified successfully. Your application is now pending review." });
    }

    /// <summary>
    /// Resends the email verification OTP for a self-registered supplier.
    /// Subject to rate-limiting (1-minute cooldown) and resend count limits.
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost("resend-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendOtp([FromBody] SupplierResendOtpRequest request)
    {
        await _authService.ResendOtpAsync(request.ContactId);
        return Ok(new { message = "A new OTP has been sent to your registered email address." });
    }

    /// <summary>
    /// Completes registration details and sets the password for an admin-invited supplier.
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost("complete-registration")]
    [AllowAnonymous]
    public async Task<IActionResult> CompleteRegistration([FromBody] SupplierCompleteRegistrationRequest request)
    {
        await _authService.CompleteRegistrationAsync(request);
        return Ok(new { message = "Registration completed successfully. Your profile is now pending review." });
    }

    /// <summary>
    /// Requests a password reset email with a token link for supplier contacts.
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] SupplierForgotPasswordRequest request)
    {
        await _authService.ForgotPasswordAsync(request.Email);
        return Ok(new { message = "If the email exists, a password reset link has been sent." });
    }

    /// <summary>
    /// Resets password using a valid reset token.
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] SupplierResetPasswordRequest request)
    {
        await _authService.ResetPasswordAsync(request.Token, request.NewPassword);
        return Ok(new { message = "Password has been reset successfully. You can now log in with your new password." });
    }
}
