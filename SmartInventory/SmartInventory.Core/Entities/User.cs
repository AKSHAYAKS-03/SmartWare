using System;
using SmartInventory.Core.Attributes;
using System.Collections.Generic;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Core.Entities;

public class User : BaseEntity, ISoftDelete
{
    [Sortable]
    public string FullName { get; set; } = string.Empty;
    [Sortable]
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool SmsEnabled { get; set; } = false;
    public bool EmailEnabled { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLogin { get; set; }


    public string? EncryptedAadhaarNumber { get; set; }

    public string? AadhaarLastFour { get; set; }

    public bool ShareContactDetails { get; set; } = false;
    public string? EmployeeId { get; set; }
    [Sortable]
    public UserStatus Status { get; set; } = UserStatus.PendingVerification;
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public string? InviteToken { get; set; }

    public DateTime? InviteTokenExpiresAt { get; set; }

    public bool IsPasswordSet { get; set; } = false;
    public DateTime? ExpiresAt { get; set; }

    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;
    public User? ApprovedBy { get; set; }
    public ICollection<User> ApprovedUsers { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
    public ICollection<UserWarehouseAccess> WarehouseAccess { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public ICollection<NotificationLog> NotificationLogs { get; set; } = [];

    public bool VerifyPassword(string password)
    {
        return BCrypt.Net.BCrypt.Verify(password, PasswordHash);
    }
}
