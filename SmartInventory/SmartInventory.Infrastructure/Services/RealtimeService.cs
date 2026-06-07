using Microsoft.AspNetCore.SignalR;
using SmartInventory.Infrastructure.Hubs;
using SmartInventory.Core.Interfaces;
using System;
using System.Threading.Tasks;

namespace SmartInventory.Infrastructure.Services;

public class RealtimeService : IRealtimeService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public RealtimeService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendNotificationToUserAsync(Guid userId, string title, string message, string type, Guid? entityId)
    {
        // Push notification message to the specific user group using SignalR
        await _hubContext.Clients.Group(userId.ToString()).SendAsync("ReceiveNotification", new
        {
            title,
            message,
            type,
            entityId,
            timestamp = DateTime.UtcNow
        });
    }
}
