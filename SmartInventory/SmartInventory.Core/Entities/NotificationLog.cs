using SmartInventory.Core.Attributes;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

public class NotificationLog : BaseEntity
{
    public NotificationChannel Channel { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    [Sortable]
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTime? SentAt { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;
}
