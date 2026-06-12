using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Interfaces;
using System.Security.Claims;

namespace SmartInventory.API.Controllers;

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

 
    [EnableRateLimiting("mutations")]
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] SupplierLoginRequest request)
    {
        var result = await _authService.LoginAsync(request, GetIpAddress());
        return Ok(result);
    }

    [EnableRateLimiting("mutations")]
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] SupplierRefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request, GetIpAddress());
        return Ok(result);
    }

    [EnableRateLimiting("mutations")]
    [HttpPost("logout")]
    [Authorize(Policy = "RequireSupplier")]
    public async Task<IActionResult> Logout([FromBody] SupplierRefreshTokenRequest request)
    {
        await _authService.RevokeTokenAsync(request.RefreshToken, GetIpAddress());
        return NoContent();
    }

  
    [EnableRateLimiting("mutations")]
    [HttpPut("change-password")]
    [Authorize(Policy = "RequireSupplier")]
    public async Task<IActionResult> ChangePassword([FromBody] SupplierChangePasswordRequest request)
    {
        var contactId = Guid.Parse(User.FindFirstValue("contactId")!);
        await _authService.ChangePasswordAsync(contactId, request);
        return NoContent();
    }


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


    [EnableRateLimiting("mutations")]
    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromBody] SupplierVerifyEmailRequest request)
    {
        await _authService.VerifyEmailAsync(request);
        return Ok(new { message = "Email verified successfully. Your application is now pending review." });
    }


    [EnableRateLimiting("mutations")]
    [HttpPost("resend-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendOtp([FromBody] SupplierResendOtpRequest request)
    {
        await _authService.ResendOtpAsync(request.ContactId);
        return Ok(new { message = "A new OTP has been sent to your registered email address." });
    }

  
    [EnableRateLimiting("mutations")]
    [HttpPost("complete-registration")]
    [AllowAnonymous]
    public async Task<IActionResult> CompleteRegistration([FromBody] SupplierCompleteRegistrationRequest request)
    {
        await _authService.CompleteRegistrationAsync(request);
        return Ok(new { message = "Registration completed successfully. Your profile is now pending review." });
    }


    [EnableRateLimiting("mutations")]
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] SupplierForgotPasswordRequest request)
    {
        await _authService.ForgotPasswordAsync(request.Email);
        return Ok(new { message = "If the email exists, a password reset link has been sent." });
    }


    [EnableRateLimiting("mutations")]
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] SupplierResetPasswordRequest request)
    {
        await _authService.ResetPasswordAsync(request.Token, request.NewPassword);
        return Ok(new { message = "Password has been reset successfully. You can now log in with your new password." });
    }
}
