using SmartInventory.Core.Attributes;
namespace SmartInventory.Core.Entities;

/// <summary>
/// Represents a portal login account for a supplier user.
/// Suppliers authenticate via this entity, NOT the main Users table.
/// </summary>
public class SupplierContact : BaseEntity
{
    [Sortable]
    public string FullName { get; set; } = string.Empty;
    [Sortable]
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }

    /// <summary>Whether this portal account is active and can log in.</summary>
    public bool IsActive { get; set; } = true;

    // Email verification details for self-registration
    public bool EmailVerified { get; set; } = false;
    public string? EmailVerifyToken { get; set; }
    public DateTime? EmailVerifyExpiresAt { get; set; }

    // OTP security - retry tracking
    public int OtpRetryCount { get; set; } = 0;
    public DateTime? LastOtpSentAt { get; set; }
    public DateTime? OtpLockedUntil { get; set; }
    public int OtpMaxRetries { get; set; } = 3;
    public int OtpMaxResends { get; set; } = 3;
    public int OtpResendCount { get; set; } = 0;

    /// <summary>UTC timestamp of last successful login.</summary>
    public DateTime? LastLoginAt { get; set; }

    // Foreign Key
    public Guid SupplierId { get; set; }

    // Navigation
    public Supplier Supplier { get; set; } = null!;
    public ICollection<SupplierRefreshToken> RefreshTokens { get; set; } = [];
}
