using System;
using System.Collections.Generic;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

/// <summary>
/// System user with authentication credentials, professional vetting, and notification preferences.
/// </summary>
public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool SmsEnabled { get; set; } = false;
    public bool EmailEnabled { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLogin { get; set; }

    // Enterprise Official Verification & Vetting
    public string? EmployeeId { get; set; } // Corporate employee code (e.g. EMP-10943)
    public UserStatus Status { get; set; } = UserStatus.PendingVerification;
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }

    // Foreign Keys
    public Guid RoleId { get; set; }

    // Navigation
    public Role Role { get; set; } = null!;
    public User? ApprovedBy { get; set; } // The Admin who vetted/approved this user
    public ICollection<User> ApprovedUsers { get; set; } = []; // Users approved by this user (if Admin)
    public ICollection<Warehouse> ApprovedWarehouses { get; set; } = []; // Warehouses approved by this user (if Admin)
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
    public ICollection<UserWarehouseAccess> WarehouseAccess { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public ICollection<NotificationLog> NotificationLogs { get; set; } = [];
}
