using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Delivery tracking for SMS/email/in-app notifications.
/// </summary>
public class NotificationLog : BaseEntity
{
    public NotificationChannel Channel { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTime? SentAt { get; set; }

    // Foreign Keys
    public Guid UserId { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
