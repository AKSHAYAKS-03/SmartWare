namespace SmartInventory.Core.DTOs.SupplierPortal;

// REQUEST DTOs
public record SupplierUpdateProfileRequest(
    string FullName,
    string? Phone,
    string? JobTitle
);

public record SupplierUploadLogoRequest(
    Stream FileStream,
    string FileName,
    string ContentType
);

// RESPONSE DTOs

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
