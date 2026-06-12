using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _poService;
    private readonly ICurrentUserService _currentUser;

    public PurchaseOrdersController(IPurchaseOrderService poService, ICurrentUserService currentUser)
    {
        _poService = poService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetPurchaseOrders([FromQuery] PurchaseOrderQueryParameters queryParams)
    {
        if (_currentUser.WarehouseId.HasValue)
        {
            queryParams.WarehouseId = _currentUser.WarehouseId.Value;
        }
        return Ok(await _poService.GetPurchaseOrdersAsync(queryParams));
    }

    [HttpPost("search")]
    [EnableRateLimiting("reports")]
    public async Task<IActionResult> SearchPurchaseOrders([FromBody] DynamicQueryRequest request)
    {
        // For dynamic search, if the user is scoped to a warehouse, we must inject a hard filter
        // to prevent them from searching other warehouses' POs.
        if (_currentUser.WarehouseId.HasValue)
        {
            request.Filters.Add(new FilterCriteria
            {
                Field = "WarehouseId",
                Operator = "eq",
                Value = _currentUser.WarehouseId.Value.ToString()
            });
        }
        return Ok(await _poService.SearchPurchaseOrdersAsync(request));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPurchaseOrder(Guid id)
    {
        var result = await _poService.GetPurchaseOrderByIdAsync(id);
        if (_currentUser.WarehouseId.HasValue && result.WarehouseId != _currentUser.WarehouseId.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, "You do not have access to this purchase order.");
        }
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "RequireManager")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> CreatePurchaseOrder([FromBody] PurchaseOrderCreateDto dto)
    {
        dto.CreatedBy = _currentUser.UserId;
        var result = await _poService.CreatePurchaseOrderAsync(dto);
        return CreatedAtAction(nameof(GetPurchaseOrder), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireManager")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> UpdatePurchaseOrder(Guid id, [FromBody] PurchaseOrderUpdateDto dto) =>
        Ok(await _poService.UpdatePurchaseOrderAsync(id, dto));

    [HttpPut("{id:guid}/submit")]
    [Authorize(Policy = "RequireManager")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> SubmitForApproval(Guid id)
    {
        var result = await _poService.SubmitForApprovalAsync(id);
        return Ok(result);
    }

    [HttpPut("{id:guid}/approve")]
    [Authorize(Policy = "RequireAdmin")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> ApprovePurchaseOrder(Guid id, [FromBody] PurchaseOrderApprovalDto dto)
    {
        // ID is now extracted from token inside the Service layer
        var result = await _poService.ApprovePurchaseOrderAsync(id, dto);
        return Ok(result);
    }

    [HttpPut("{id:guid}/cancel")]
    [Authorize(Policy = "RequireManager")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> CancelPurchaseOrder(Guid id)
    {
        var result = await _poService.CancelPurchaseOrderAsync(id, _currentUser.UserId);
        return Ok(result);
    }

    [HttpPost("{id:guid}/grn")]
    [Authorize(Policy = "RequireStaff")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> ReceiveGoods(Guid id, [FromBody] GoodsReceiptCreateDto dto)
    {
        dto.PurchaseOrderId = id;
        dto.ReceivedBy = _currentUser.UserId;
        var result = await _poService.ReceiveGoodsAsync(dto);
        return Ok(result);
    }

    [HttpPost("{id:guid}/grn/scan")]
    [Authorize(Policy = "RequireStaff")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> ReceiveGoodsByScan(Guid id, [FromBody] BarcodeGoodsReceiptCreateDto dto)
    {
        dto.PurchaseOrderId = id;
        dto.ReceivedBy = _currentUser.UserId;
        var result = await _poService.ReceiveGoodsByBarcodeAsync(dto);
        return Ok(result);
    }

    [HttpPost("{id:guid}/grn/bulk")]
    [Authorize(Policy = "RequireStaff")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> ReceiveGoodsBulk(Guid id, [FromForm] Guid warehouseId, [FromForm] string? notes, [FromForm] string? idempotencyKey, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("CSV file is required.");

        var dto = new GoodsReceiptCreateDto
        {
            PurchaseOrderId = id,
            ReceivedBy = _currentUser.UserId,
            WarehouseId = warehouseId,
            Notes = notes,
            IdempotencyKey = idempotencyKey,
            Items = []
        };

        using var stream = file.OpenReadStream();
        using var reader = new System.IO.StreamReader(stream);
        using var csv = new CsvHelper.CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture);

        var records = csv.GetRecordsAsync<GoodsReceiptItemDto>();
        await foreach (var record in records)
        {
            dto.Items.Add(record);
        }

        var result = await _poService.ReceiveGoodsAsync(dto);
        return Ok(result);
    }

    [HttpGet("{id:guid}/grn")]
    public async Task<IActionResult> GetGoodsReceipts(Guid id) =>
        Ok(await _poService.GetGoodsReceiptsAsync(id));

    [HttpPost("receipts/{receiptId:guid}/cancel")]
    [Authorize(Policy = "RequireManager")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> CancelGoodsReceipt(Guid receiptId)
    {
        var result = await _poService.CancelGoodsReceiptAsync(receiptId, _currentUser.UserId);
        return Ok(new { success = result, message = "Goods Receipt successfully reversed." });
    }
}
