using SmartInventory.Core.DTOs.SupplierPortal;

namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Purchase order operations available to a supplier via the portal.
/// All methods require supplierId from JWT claims — never from request body.
/// </summary>
public interface ISupplierPurchaseOrderService
{
    /// <summary>Returns all POs raised against this supplier. Filtered to their SupplierId only.</summary>
    Task<List<SupplierPOListItemDto>> GetMyPurchaseOrdersAsync(Guid supplierId);

    /// <summary>Returns full details of a single PO. Validates it belongs to this supplier.</summary>
    Task<SupplierPODetailDto> GetPurchaseOrderDetailAsync(Guid supplierId, Guid poId);

    /// <summary>Supplier accepts or declines a PO that is in Submitted/Approved state.</summary>
    Task RespondToPurchaseOrderAsync(Guid supplierId, Guid poId, SupplierRespondToPORequest request);

    /// <summary>Supplier updates the expected delivery date (only while PO is not dispatched).</summary>
    Task UpdateExpectedDeliveryAsync(Guid supplierId, Guid poId, SupplierUpdateDeliveryDateRequest request);

    /// <summary>Supplier marks the order as dispatched, optionally providing a tracking number.</summary>
    Task MarkAsDispatchedAsync(Guid supplierId, Guid poId, SupplierMarkDispatchedRequest request);

    /// <summary>Creates a supplier ASN/shipment with line-level dispatched quantities.</summary>
    Task<SupplierShipmentResponseDto> CreateShipmentAsync(Guid supplierId, Guid poId, SupplierCreateShipmentRequest request);

    /// <summary>Lists all shipments for a purchase order.</summary>
    Task<List<SupplierShipmentResponseDto>> GetShipmentsAsync(Guid supplierId, Guid poId);
}
