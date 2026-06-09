namespace SmartInventory.Core.DTOs.SupplierPortal;

// REQUEST DTOs

//Supplier updates the price and lead time for a product they supply
public record SupplierUpdateCatalogueItemRequest(
    decimal UnitPrice,
    int LeadTimeDays,
    int MinOrderQuantity
);

//Supplier adds a new product to their catalogue
public record SupplierAddCatalogueItemRequest(
    Guid ProductId,
    decimal UnitPrice,
    int LeadTimeDays,
    int MinOrderQuantity
);

// RESPONSE DTOs

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
