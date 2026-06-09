namespace SmartInventory.Core.Entities;

public class SupplierRefreshToken : BaseEntity
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public string? RevokedReason { get; set; }
    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }

    
    public Guid SupplierContactId { get; set; }

    
    public SupplierContact SupplierContact { get; set; } = null!;
}
