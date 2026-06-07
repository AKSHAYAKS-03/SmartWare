namespace SmartInventory.Core.DTOs.SupplierPortal;

// ──────────────────────────────────────────────────────────────────────────────
// REQUEST DTOs
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Supplier updates their own contact details in the portal.</summary>
public record SupplierUpdateProfileRequest(
    string FullName,
    string? Phone,
    string? JobTitle
);

/// <summary>Supplier uploads a new logo for their profile.</summary>
public record SupplierUploadLogoRequest(
    Stream FileStream,
    string FileName,
    string ContentType
);

// ──────────────────────────────────────────────────────────────────────────────
// RESPONSE DTOs
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Supplier profile view from the portal — only their own data.</summary>
public record SupplierProfileDto(
    Guid SupplierId,
    string Name,
    string Code,
    string? Address,
    string ContactPersonName,
    string ContactEmail,
    string? ContactPhone,
    string? JobTitle,
    int LeadTimeDays,
    decimal Rating,
    bool IsActive
);
