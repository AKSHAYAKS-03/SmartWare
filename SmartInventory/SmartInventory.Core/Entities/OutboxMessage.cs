using System;
using SmartInventory.Core.Attributes;

namespace SmartInventory.Core.Entities;

public class OutboxMessage : BaseEntity
{
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty; 
    [Sortable]
    public string Status { get; set; } = "Pending"; // Pending, Processed, Failed
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTime? ProcessedAt { get; set; }
}

public class OutboxNotificationPayload
{
    public Guid UserId { get; set; }
    public SmartInventory.Core.Enums.NotificationChannel Channel { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public Guid? NotificationId { get; set; }
}
