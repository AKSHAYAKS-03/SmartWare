using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Interfaces;
using System.Security.Claims;

namespace SmartInventory.API.Controllers;


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


    [HttpGet]
    public async Task<IActionResult> GetMyPurchaseOrders()
    {
        var result = await _service.GetMyPurchaseOrdersAsync(GetSupplierId());
        return Ok(result);
    }


    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var result = await _service.GetPurchaseOrderDetailAsync(GetSupplierId(), id);
        return Ok(result);
    }


    [EnableRateLimiting("mutations")]
    [HttpPost("{id:guid}/respond")]
    public async Task<IActionResult> Respond(Guid id, [FromBody] SupplierRespondToPORequest request)
    {
        await _service.RespondToPurchaseOrderAsync(GetSupplierId(), id, request);
        return Ok(new { message = request.Accept ? "Purchase order accepted." : "Purchase order declined." });
    }


    [EnableRateLimiting("mutations")]
    [HttpPut("{id:guid}/delivery-date")]
    public async Task<IActionResult> UpdateDeliveryDate(Guid id, [FromBody] SupplierUpdateDeliveryDateRequest request)
    {
        await _service.UpdateExpectedDeliveryAsync(GetSupplierId(), id, request);
        return Ok(new { message = "Expected delivery date updated." });
    }

    [EnableRateLimiting("mutations")]
    [HttpPost("{id:guid}/dispatch")]
    public async Task<IActionResult> MarkDispatched(Guid id, [FromBody] SupplierMarkDispatchedRequest request)
    {
        var shipment = await _service.CreateShipmentAsync(GetSupplierId(), id,
            new SupplierCreateShipmentRequest(null, null, request.SupplierNotes, null));
        return Ok(new { message = "Order marked as dispatched.", shipment });
    }

    [EnableRateLimiting("mutations")]
    [HttpPost("{id:guid}/shipments")]
    public async Task<IActionResult> CreateShipment(Guid id, [FromBody] SupplierCreateShipmentRequest request)
    {
        var result = await _service.CreateShipmentAsync(GetSupplierId(), id, request);
        return Ok(result);
    }

    [HttpGet("{id:guid}/shipments")]
    public async Task<IActionResult> GetShipments(Guid id)
    {
        return Ok(await _service.GetShipmentsAsync(GetSupplierId(), id));
    }
}
