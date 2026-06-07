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
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserService _currentUser;

    public NotificationsController(INotificationService notificationService, ICurrentUserService currentUser)
    {
        _notificationService = notificationService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetInbox([FromQuery] QueryParameters queryParams) =>
        Ok(await _notificationService.GetUserNotificationsAsync(_currentUser.UserId, queryParams));

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount() =>
        Ok(new { count = await _notificationService.GetUnreadCountAsync(_currentUser.UserId) });

    [EnableRateLimiting("mutations")]

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        await _notificationService.MarkAsReadAsync(id, _currentUser.UserId);
        return NoContent();
    }

    [EnableRateLimiting("mutations")]

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await _notificationService.MarkAllAsReadAsync(_currentUser.UserId);
        return NoContent();
    }
}
