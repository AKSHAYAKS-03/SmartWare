using System;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.DTOs;

public class NotificationResponseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public string ChannelName => Channel.ToString();
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationQueryParameters : QueryParameters
{
    public Guid? UserId { get; set; }
    public bool? IsRead { get; set; }
    public NotificationChannel? Channel { get; set; }
}

public class NotificationLogResponseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public string ChannelName => Channel.ToString();
    public string EventType { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
