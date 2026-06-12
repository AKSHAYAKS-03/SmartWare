using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    public SuppliersController(ISupplierService supplierService) =>
        _supplierService = supplierService;

    [HttpGet]
    public async Task<IActionResult> GetSuppliers([FromQuery] SupplierQueryParameters queryParams) =>
        Ok(await _supplierService.GetSuppliersAsync(queryParams));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSupplier(Guid id) =>
        Ok(await _supplierService.GetSupplierByIdAsync(id));

    [EnableRateLimiting("mutations")]

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> UpdateSupplier(Guid id, [FromBody] SupplierUpdateDto dto) =>
        Ok(await _supplierService.UpdateSupplierAsync(id, dto));

    [EnableRateLimiting("mutations")]

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeleteSupplier(Guid id)
    {
        await _supplierService.DeleteSupplierAsync(id);
        return NoContent();
    }

    [HttpGet("{id:guid}/products")]
    public async Task<IActionResult> GetSupplierProducts(Guid id) =>
        Ok(await _supplierService.GetSupplierProductsAsync(id));

    [EnableRateLimiting("mutations")]

    [HttpPost("{id:guid}/products")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> AddSupplierProduct(Guid id, [FromBody] SupplierProductCreateDto dto)
    {
        dto.SupplierId = id;
        var result = await _supplierService.AddSupplierProductAsync(dto);
        return CreatedAtAction(nameof(GetSupplierProducts), new { id }, result);
    }

    [EnableRateLimiting("mutations")]

    [HttpPut("products/{supplierProductId:guid}")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> UpdateSupplierProduct(Guid supplierProductId, [FromBody] SupplierProductUpdateDto dto) =>
        Ok(await _supplierService.UpdateSupplierProductAsync(supplierProductId, dto));

    [EnableRateLimiting("mutations")]

    [HttpDelete("products/{supplierProductId:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> RemoveSupplierProduct(Guid supplierProductId)
    {
        await _supplierService.RemoveSupplierProductAsync(supplierProductId);
        return NoContent();
    }

    [HttpGet("{id:guid}/performance")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<IActionResult> GetSupplierPerformance(Guid id) =>
        Ok(await _supplierService.GetSupplierPerformanceAsync(id));

    [EnableRateLimiting("mutations")]

    [HttpPost("{id:guid}/recalculate-rating")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> RecalculateRating(Guid id)
    {
        await _supplierService.RecalculateSupplierRatingAsync(id);
        return NoContent();
    }

    /// Invites a supplier to register, creating a record in InviteSent state and generating a secure invite token.
    [EnableRateLimiting("mutations")]
    [HttpPost("invite")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> InviteSupplier([FromBody] SupplierInviteRequest request)
    {
        var result = await _supplierService.InviteSupplierAsync(request);
        return Ok(result);
    }

    [HttpGet("pending-reviews")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> GetPendingReviews()
    {
        var result = await _supplierService.GetPendingReviewsAsync();
        return Ok(result);
    }

    [EnableRateLimiting("mutations")]
    [HttpPost("{id:guid}/review")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> ReviewSupplier(Guid id, [FromBody] SupplierReviewRequest request)
    {
        var result = await _supplierService.ReviewSupplierAsync(id, request);
        return Ok(result);
    }


    [EnableRateLimiting("mutations")]
    [HttpPost("{id:guid}/suspend")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> SuspendSupplier(Guid id, [FromBody] SupplierSuspendRequest request)
    {
        await _supplierService.SuspendSupplierAsync(id, request.Reason);
        return NoContent();
    }

  
    [EnableRateLimiting("mutations")]
    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> ActivateSupplier(Guid id)
    {
        await _supplierService.ActivateSupplierAsync(id);
        return NoContent();
    }
}
