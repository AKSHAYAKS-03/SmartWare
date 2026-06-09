using SmartInventory.Core.Attributes;
namespace SmartInventory.Core.Entities;


public class SupplierContact : BaseEntity
{
    [Sortable]
    public string FullName { get; set; } = string.Empty;
    [Sortable]
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }

    public bool IsActive { get; set; } = true;

    public bool EmailVerified { get; set; } = false;
    public string? EmailVerifyToken { get; set; }
    public DateTime? EmailVerifyExpiresAt { get; set; }

    public int OtpRetryCount { get; set; } = 0;
    public DateTime? LastOtpSentAt { get; set; }
    public DateTime? OtpLockedUntil { get; set; }
    public int OtpMaxRetries { get; set; } = 3;
    public int OtpMaxResends { get; set; } = 3;
    public int OtpResendCount { get; set; } = 0;

    public DateTime? LastLoginAt { get; set; }

    public Guid SupplierId { get; set; }

    public Supplier Supplier { get; set; } = null!;
    public ICollection<SupplierRefreshToken> RefreshTokens { get; set; } = [];
}
