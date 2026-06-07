using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ICurrentUserService _currentUser;

    public UsersController(IUserService userService, ICurrentUserService currentUser)
    {
        _userService = userService;
        _currentUser = currentUser;
    }

    /// <summary>Returns the currently authenticated user's own profile.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMyProfile() =>
        Ok(await _userService.GetUserByIdAsync(_currentUser.UserId));

    [HttpGet]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> GetUsers([FromQuery] QueryParameters queryParams) =>
        Ok(await _userService.GetUsersAsync(queryParams));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> GetUser(Guid id) =>
        Ok(await _userService.GetUserByIdAsync(id));

    [EnableRateLimiting("mutations")]

    [HttpPost]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> CreateUser([FromBody] UserCreateDto dto)
    {
        var result = await _userService.CreateUserAsync(dto);
        return CreatedAtAction(nameof(GetUser), new { id = result.Id }, result);
    }

    [EnableRateLimiting("mutations")]

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UserUpdateDto dto) =>
        Ok(await _userService.UpdateUserAsync(id, dto));

    [EnableRateLimiting("mutations")]

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeactivateUser(Guid id)
    {
        await _userService.DeactivateUserAsync(id);
        return NoContent();
    }

    [EnableRateLimiting("mutations")]

    [HttpPut("{id:guid}/approve")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> ApproveUser(Guid id) =>
        Ok(await _userService.ApproveUserAsync(id, _currentUser.UserId));

    [EnableRateLimiting("mutations")]

    [HttpPut("{id:guid}/notifications")]
    public async Task<IActionResult> UpdateNotifications(
        Guid id, [FromBody] NotificationPreferencesDto dto)
    {
        // Users can only update their own preferences unless they're Admin
        if (id != _currentUser.UserId && !_currentUser.IsInRole("Admin"))
            return Forbid();

        await _userService.UpdateNotificationPreferencesAsync(id, dto.SmsEnabled, dto.EmailEnabled);
        return NoContent();
    }

    /// <summary>
    /// Regenerates the invite token and resends the welcome email to the employee.
    /// Use when: the employee never received the email, or the 48-hour link has expired.
    /// Only valid for accounts that have NOT yet set their password (IsPasswordSet = false).
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost("{id:guid}/resend-invite")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> ResendInvite(Guid id)
    {
        await _userService.ResendInviteAsync(id);
        return Ok(new { message = "Invitation email has been resent. The new link is valid for 48 hours." });
    }
}

/// <summary>Simple DTO for notification preference updates.</summary>
public class NotificationPreferencesDto
{
    public bool SmsEnabled { get; set; }
    public bool EmailEnabled { get; set; }
}
