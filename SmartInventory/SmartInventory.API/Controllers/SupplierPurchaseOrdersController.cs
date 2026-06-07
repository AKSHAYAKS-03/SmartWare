using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Interfaces;
using System.Security.Claims;

namespace SmartInventory.API.Controllers;

/// <summary>
/// Supplier portal purchase order endpoints.
/// Route prefix: /api/supplier/purchase-orders
///
/// GET    /api/supplier/purchase-orders           — List my POs [Supplier]
/// GET    /api/supplier/purchase-orders/{id}      — PO detail [Supplier]
/// POST   /api/supplier/purchase-orders/{id}/respond — Accept/decline [Supplier]
/// PUT    /api/supplier/purchase-orders/{id}/delivery-date — Update delivery date [Supplier]
/// POST   /api/supplier/purchase-orders/{id}/dispatch — Mark dispatched [Supplier]
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/supplier/purchase-orders")]
[Authorize(Policy = "RequireSupplier")]
public class SupplierPurchaseOrdersController : ControllerBase
{
    private readonly ISupplierPurchaseOrderService _service;

    public SupplierPurchaseOrdersController(ISupplierPurchaseOrderService service)
    {
        _service = service;
    }

    private Guid GetSupplierId() =>
        Guid.Parse(User.FindFirstValue("supplierId")!);

    /// <summary>
    /// Returns all purchase orders raised against this supplier.
    /// Scoped to the authenticated supplier — no other supplier POs are visible.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyPurchaseOrders()
    {
        var result = await _service.GetMyPurchaseOrdersAsync(GetSupplierId());
        return Ok(result);
    }

    /// <summary>
    /// Returns full details of a specific PO including line items.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var result = await _service.GetPurchaseOrderDetailAsync(GetSupplierId(), id);
        return Ok(result);
    }

    /// <summary>
    /// Supplier accepts or declines a PO.
    /// Only valid for POs in Submitted or Approved status that have not been responded to yet.
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost("{id:guid}/respond")]
    public async Task<IActionResult> Respond(Guid id, [FromBody] SupplierRespondToPORequest request)
    {
        await _service.RespondToPurchaseOrderAsync(GetSupplierId(), id, request);
        return Ok(new { message = request.Accept ? "Purchase order accepted." : "Purchase order declined." });
    }

    /// <summary>
    /// Supplier updates the expected delivery date.
    /// Not allowed after the order has been dispatched.
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPut("{id:guid}/delivery-date")]
    public async Task<IActionResult> UpdateDeliveryDate(Guid id, [FromBody] SupplierUpdateDeliveryDateRequest request)
    {
        await _service.UpdateExpectedDeliveryAsync(GetSupplierId(), id, request);
        return Ok(new { message = "Expected delivery date updated." });
    }

    /// <summary>
    /// Supplier marks the order as dispatched with an optional tracking number.
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost("{id:guid}/dispatch")]
    public async Task<IActionResult> MarkDispatched(Guid id, [FromBody] SupplierMarkDispatchedRequest request)
    {
        var shipment = await _service.CreateShipmentAsync(GetSupplierId(), id,
            new SupplierCreateShipmentRequest(request.TrackingNumber, null, null, request.SupplierNotes, null));
        return Ok(new { message = "Order marked as dispatched.", shipment });
    }

    /// <summary>Creates a partial or full supplier ASN/shipment.</summary>
    [EnableRateLimiting("mutations")]
    [HttpPost("{id:guid}/shipments")]
    public async Task<IActionResult> CreateShipment(Guid id, [FromBody] SupplierCreateShipmentRequest request)
    {
        var result = await _service.CreateShipmentAsync(GetSupplierId(), id, request);
        return Ok(result);
    }

    /// <summary>Lists all shipments for a purchase order.</summary>
    [HttpGet("{id:guid}/shipments")]
    public async Task<IActionResult> GetShipments(Guid id)
    {
        return Ok(await _service.GetShipmentsAsync(GetSupplierId(), id));
    }
}
