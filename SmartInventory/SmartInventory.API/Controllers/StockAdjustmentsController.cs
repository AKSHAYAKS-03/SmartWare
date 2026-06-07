using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class StockAdjustmentsController : ControllerBase
{
    private readonly IStockAdjustmentService _adjustmentService;
    private readonly ICurrentUserService _currentUser;

    public StockAdjustmentsController(IStockAdjustmentService adjustmentService, ICurrentUserService currentUser)
    {
        _adjustmentService = adjustmentService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = "RequireViewer")]
    public async Task<IActionResult> GetAdjustments([FromQuery] StockAdjustmentQueryParameters queryParams)
    {
        if (_currentUser.WarehouseId.HasValue)
        {
            queryParams.WarehouseId = _currentUser.WarehouseId.Value;
        }
        return Ok(await _adjustmentService.GetAdjustmentsAsync(queryParams));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<IActionResult> GetAdjustment(Guid id)
    {
        var result = await _adjustmentService.GetAdjustmentByIdAsync(id);
        if (_currentUser.WarehouseId.HasValue && result.WarehouseId != _currentUser.WarehouseId.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, "You do not have access to this stock adjustment.");
        }
        return Ok(result);
    }

    [EnableRateLimiting("mutations")]

    [HttpPost]
    [Authorize(Policy = "RequireStaff")]
    public async Task<IActionResult> CreateAdjustment([FromBody] StockAdjustmentCreateDto dto)
    {
        dto.PerformedBy = _currentUser.UserId;
        var result = await _adjustmentService.CreateAdjustmentAsync(dto);
        return CreatedAtAction(nameof(GetAdjustment), new { id = result.Id }, result);
    }

    [EnableRateLimiting("mutations")]

    [HttpPut("{id:guid}/approve")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> ApproveAdjustment(Guid id, [FromBody] StockAdjustmentApprovalDto dto)
    {
        var result = await _adjustmentService.ApproveAdjustmentAsync(id, dto);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "RequireManager")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> CancelAdjustment(Guid id)
    {
        var result = await _adjustmentService.CancelStockAdjustmentAsync(id, _currentUser.UserId);
        return Ok(new { success = result, message = "Stock adjustment successfully reversed." });
    }
}
