using System;
using SmartInventory.Core.Attributes;
using System.Collections.Generic;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Core.Entities;

/// <summary>
/// System user with authentication credentials, professional vetting, and notification preferences.
/// </summary>
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

    // Highly sensitive PII – stored encrypted (AES‑256) and only the last 4 digits in plain text
    public string? EncryptedAadhaarNumber { get; set; }

    // Masked version for UI (e.g. "XXXX‑XXXX‑1234")
    public string? AadhaarLastFour { get; set; }

    // Consent flag – controls whether other users can view phone/email of this user
    public bool ShareContactDetails { get; set; } = false;
    public string? EmployeeId { get; set; } // Corporate employee code (e.g. EMP-10943)
    [Sortable]
    public UserStatus Status { get; set; } = UserStatus.PendingVerification;
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }

    // ── Invitation / First-Login Flow ─────────────────────────────────────────
    /// <summary>
    /// One-time cryptographically secure token sent via email when the account is created.
    /// The employee uses this token to set their own password.
    /// Null once the password has been set (invalidated on use).
    /// </summary>
    public string? InviteToken { get; set; }

    /// <summary>Expiry for the invite link. Default window: 48 hours from account creation.</summary>
    public DateTime? InviteTokenExpiresAt { get; set; }

    /// <summary>
    /// False until the employee successfully sets their own password via /Auth/set-password.
    /// Prevents login before password is configured.
    /// </summary>
    public bool IsPasswordSet { get; set; } = false;
    public DateTime? ExpiresAt { get; set; }

    // Foreign Keys
    public Guid RoleId { get; set; }

    // Navigation
    public Role Role { get; set; } = null!;
    public User? ApprovedBy { get; set; } // The Admin who vetted/approved this user
    public ICollection<User> ApprovedUsers { get; set; } = []; // Users approved by this user (if Admin)
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
