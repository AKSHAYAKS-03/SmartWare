using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Supplier profile with contact, payment, and performance info.
/// </summary>
public class Supplier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int LeadTimeDays { get; set; }
    public PaymentTerms PaymentTerms { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal Rating { get; set; } // 0.0 - 5.0
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<SupplierProduct> SupplierProducts { get; set; } = [];
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = [];
    public ICollection<SupplierPerformanceLog> PerformanceLogs { get; set; } = [];
}
