using SmartInventory.Core.Attributes;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Supplier profile with contact, payment, and performance info.
/// </summary>
public class Supplier : BaseEntity, ISoftDelete
{
    [Sortable]
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = null!;
    public string? GSTIN { get; set; }
    public string? PAN { get; set; }
    public string? ContactPerson { get; set; }
    [Sortable]
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Address { get; set; } = string.Empty;
    public int LeadTimeDays { get; set; }
    public PaymentTerms PaymentTerms { get; set; }
    public decimal CreditLimit { get; set; }
    [Sortable]
    public decimal Rating { get; set; } // 0.0 - 5.0
    public bool IsActive { get; set; } = true;

    // Onboarding status and registration source details
    [Sortable]
    public SupplierStatus Status { get; set; } = SupplierStatus.Registered;
    public RegistrationSource RegistrationSource { get; set; } = RegistrationSource.SelfRegistered;
    public string? InviteToken { get; set; }
    public DateTime? InviteTokenExpiresAt { get; set; }
    public DateTime? AgreementSignedAt { get; set; }
    public string? AgreementSignedIp { get; set; }
    public string? RejectionReason { get; set; }
    public string? SuspensionReason { get; set; }
    public string? InfoRequestedMessage { get; set; }

    // Navigation
    public ICollection<SupplierProduct> SupplierProducts { get; set; } = [];
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = [];
    public ICollection<SupplierPerformanceLog> PerformanceLogs { get; set; } = [];
    public ICollection<SupplierContact> Contacts { get; set; } = [];
    public ICollection<SupplierInvoice> Invoices { get; set; } = [];
}
