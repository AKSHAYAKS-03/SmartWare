using SmartInventory.Core.DTOs.SupplierPortal;

namespace SmartInventory.Core.Interfaces;
public interface ISupplierPurchaseOrderService
{
    Task<List<SupplierPOListItemDto>> GetMyPurchaseOrdersAsync(Guid supplierId);

    Task<SupplierPODetailDto> GetPurchaseOrderDetailAsync(Guid supplierId, Guid poId);

    Task RespondToPurchaseOrderAsync(Guid supplierId, Guid poId, SupplierRespondToPORequest request);

    Task UpdateExpectedDeliveryAsync(Guid supplierId, Guid poId, SupplierUpdateDeliveryDateRequest request);

    Task MarkAsDispatchedAsync(Guid supplierId, Guid poId, SupplierMarkDispatchedRequest request);

    Task<SupplierShipmentResponseDto> CreateShipmentAsync(Guid supplierId, Guid poId, SupplierCreateShipmentRequest request);

    Task<List<SupplierShipmentResponseDto>> GetShipmentsAsync(Guid supplierId, Guid poId);
}
