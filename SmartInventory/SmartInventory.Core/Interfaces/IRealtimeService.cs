namespace SmartInventory.Core.Interfaces;

public interface IRealtimeService
{
    /// <summary>
    /// Pushes a real-time notification to a connected user.
    /// </summary>
    Task SendNotificationToUserAsync(Guid userId, string title, string message, string type, Guid? entityId);
}
