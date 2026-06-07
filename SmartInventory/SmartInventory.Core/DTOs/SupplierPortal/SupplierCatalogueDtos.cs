namespace SmartInventory.Core.DTOs.SupplierPortal;

// ──────────────────────────────────────────────────────────────────────────────
// REQUEST DTOs
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Supplier updates the price and lead time for a product they supply.</summary>
public record SupplierUpdateCatalogueItemRequest(
    decimal UnitPrice,
    int LeadTimeDays,
    int MinOrderQuantity
);

/// <summary>Supplier adds a new product to their catalogue.</summary>
public record SupplierAddCatalogueItemRequest(
    Guid ProductId,
    decimal UnitPrice,
    int LeadTimeDays,
    int MinOrderQuantity
);

// ──────────────────────────────────────────────────────────────────────────────
// RESPONSE DTOs
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>A product in the supplier's catalogue as viewed from the portal.</summary>
public record SupplierCatalogueItemDto(
    Guid SupplierProductId,
    Guid ProductId,
    string ProductName,
    string Sku,
    string Category,
    decimal UnitPrice,
    int LeadTimeDays,
    int MinOrderQuantity,
    bool IsPreferred,
    bool IsActive
);
