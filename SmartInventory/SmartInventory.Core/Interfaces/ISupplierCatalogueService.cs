using SmartInventory.Core.DTOs.SupplierPortal;

namespace SmartInventory.Core.Interfaces;
public interface ISupplierCatalogueService
{
    Task<List<SupplierCatalogueItemDto>> GetMyCatalogueAsync(Guid supplierId);

    Task UpdateCatalogueItemAsync(Guid supplierId, Guid supplierProductId, SupplierUpdateCatalogueItemRequest request);

    Task<SupplierCatalogueItemDto> AddCatalogueItemAsync(Guid supplierId, SupplierAddCatalogueItemRequest request);

    Task DeactivateCatalogueItemAsync(Guid supplierId, Guid supplierProductId);
}
