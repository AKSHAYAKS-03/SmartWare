using SmartInventory.Core.DTOs;
using SmartInventory.Core.DTOs.SupplierPortal;

namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Supplier directory and relationship management service.
/// Handles supplier CRUD, supplier-product mappings, and performance rating calculations.
/// </summary>
public interface ISupplierService
{
    Task<SupplierResponseDto> UpdateSupplierAsync(Guid supplierId, SupplierUpdateDto dto);
    Task DeleteSupplierAsync(Guid supplierId);
    Task<SupplierResponseDto> GetSupplierByIdAsync(Guid supplierId);
    Task<PagedResult<SupplierResponseDto>> GetSuppliersAsync(SupplierQueryParameters queryParams);

    Task<SupplierProductResponseDto> AddSupplierProductAsync(SupplierProductCreateDto dto);
    Task<SupplierProductResponseDto> UpdateSupplierProductAsync(Guid supplierProductId, SupplierProductUpdateDto dto);
    Task RemoveSupplierProductAsync(Guid supplierProductId);
    Task<IEnumerable<SupplierProductResponseDto>> GetSupplierProductsAsync(Guid supplierId);

    Task<IEnumerable<SupplierPerformanceLogResponseDto>> GetSupplierPerformanceAsync(Guid supplierId);
    Task RecalculateSupplierRatingAsync(Guid supplierId);

    // Onboarding Actions
    Task<SupplierResponseDto> InviteSupplierAsync(SupplierInviteRequest request);
    Task<SupplierResponseDto> ReviewSupplierAsync(Guid supplierId, SupplierReviewRequest request);
    Task SuspendSupplierAsync(Guid supplierId, string reason);
    Task ActivateSupplierAsync(Guid supplierId);
    Task<IEnumerable<SupplierResponseDto>> GetPendingReviewsAsync();
}
