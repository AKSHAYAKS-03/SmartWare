using SmartInventory.Core.DTOs.SupplierPortal;

namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Product catalogue operations for a supplier in the portal.
/// Suppliers can only see and edit their own catalogue entries.
/// </summary>
public interface ISupplierCatalogueService
{
    /// <summary>Returns all products this supplier supplies (their catalogue).</summary>
    Task<List<SupplierCatalogueItemDto>> GetMyCatalogueAsync(Guid supplierId);

    /// <summary>Supplier updates price, lead time, MOQ for one of their products.</summary>
    Task UpdateCatalogueItemAsync(Guid supplierId, Guid supplierProductId, SupplierUpdateCatalogueItemRequest request);

    /// <summary>Supplier adds a new product to their catalogue (creates a new SupplierProduct record).</summary>
    Task<SupplierCatalogueItemDto> AddCatalogueItemAsync(Guid supplierId, SupplierAddCatalogueItemRequest request);

    /// <summary>Supplier deactivates a product from their catalogue (sets IsActive = false).</summary>
    Task DeactivateCatalogueItemAsync(Guid supplierId, Guid supplierProductId);
}
