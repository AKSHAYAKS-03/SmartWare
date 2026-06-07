using System;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.DTOs;

#region Supplier DTOs
public class SupplierCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? GSTIN { get; set; }
    public string? PAN { get; set; }
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int LeadTimeDays { get; set; }
    public PaymentTerms PaymentTerms { get; set; }
    public decimal CreditLimit { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SupplierUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? GSTIN { get; set; }
    public string? PAN { get; set; }
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int LeadTimeDays { get; set; }
    public PaymentTerms PaymentTerms { get; set; }
    public decimal CreditLimit { get; set; }
    public bool IsActive { get; set; }
}

public class SupplierResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? GSTIN { get; set; }
    public string? PAN { get; set; }
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int LeadTimeDays { get; set; }
    public PaymentTerms PaymentTerms { get; set; }
    public string PaymentTermsName => PaymentTerms.ToString();
    public decimal CreditLimit { get; set; }
    public decimal Rating { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    // Onboarding properties
    public SupplierStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public RegistrationSource RegistrationSource { get; set; }
    public string RegistrationSourceName => RegistrationSource.ToString();
    public string? InviteToken { get; set; }
    public DateTime? InviteTokenExpiresAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? SuspensionReason { get; set; }
    public string? InfoRequestedMessage { get; set; }
    public DateTime? AgreementSignedAt { get; set; }
    public string? AgreementSignedIp { get; set; }
}

public class SupplierSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class SupplierQueryParameters : QueryParameters
{
    public bool? IsActive { get; set; }
    public decimal? MinRating { get; set; }
}
#endregion

#region SupplierProduct DTOs
public class SupplierProductCreateDto
{
    public Guid SupplierId { get; set; }
    public Guid ProductId { get; set; }
    public decimal UnitPrice { get; set; }
    public int LeadTimeDays { get; set; }
    public int MinOrderQuantity { get; set; }
    public bool IsPreferred { get; set; } = false;
}

public class SupplierProductUpdateDto
{
    public decimal UnitPrice { get; set; }
    public int LeadTimeDays { get; set; }
    public int MinOrderQuantity { get; set; }
    public bool IsPreferred { get; set; }
}

public class SupplierProductResponseDto
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSKU { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int LeadTimeDays { get; set; }
    public int MinOrderQuantity { get; set; }
    public bool IsPreferred { get; set; }
    public DateTime CreatedAt { get; set; }
}
#endregion

#region SupplierPerformanceLog DTOs
public class SupplierPerformanceLogResponseDto
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid PurchaseOrderId { get; set; }
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public int PromisedDays { get; set; }
    public int ActualDays { get; set; }
    public decimal FillRate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
#endregion
