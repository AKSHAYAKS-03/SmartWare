namespace SmartInventory.Core.Entities;

/// <summary>
/// Refresh token for supplier portal JWT sessions.
/// Isolated from the main RefreshToken table used by internal users.
/// </summary>
public class SupplierRefreshToken : BaseEntity
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public string? RevokedReason { get; set; }
    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }

    // Foreign Key
    public Guid SupplierContactId { get; set; }

    // Navigation
    public SupplierContact SupplierContact { get; set; } = null!;
}
