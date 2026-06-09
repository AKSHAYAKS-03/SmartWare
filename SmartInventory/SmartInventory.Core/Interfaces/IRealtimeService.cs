namespace SmartInventory.Core.Interfaces;

public interface IRealtimeService
{
    // Pushes a real-time notification to a connected user.
    Task SendNotificationToUserAsync(Guid userId, string title, string message, string type, Guid? entityId);
}
