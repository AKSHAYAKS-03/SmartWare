using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Interfaces;
using System.Security.Claims;

namespace SmartInventory.API.Controllers;

/// <summary>
/// Supplier portal catalogue management endpoints.
/// Route prefix: /api/supplier/catalogue
///
/// GET    /api/supplier/catalogue            — List my catalogue [Supplier]
/// POST   /api/supplier/catalogue            — Add a product [Supplier]
/// PUT    /api/supplier/catalogue/{id}       — Update price/lead time [Supplier]
/// DELETE /api/supplier/catalogue/{id}       — Deactivate product [Supplier]
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/supplier/catalogue")]
[Authorize(Policy = "RequireSupplier")]
public class SupplierCatalogueController : ControllerBase
{
    private readonly ISupplierCatalogueService _service;

    public SupplierCatalogueController(ISupplierCatalogueService service)
    {
        _service = service;
    }

    private Guid GetSupplierId() => Guid.Parse(User.FindFirstValue("supplierId")!);

    /// <summary>
    /// Returns all products in this supplier's catalogue.
    /// Other suppliers' catalogue entries are not visible.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyCatalogue()
    {
        var result = await _service.GetMyCatalogueAsync(GetSupplierId());
        return Ok(result);
    }

    /// <summary>
    /// Adds a new product to the supplier's catalogue.
    /// The product must already exist in the master catalogue.
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost]
    public async Task<IActionResult> AddItem([FromBody] SupplierAddCatalogueItemRequest request)
    {
        var result = await _service.AddCatalogueItemAsync(GetSupplierId(), request);
        return CreatedAtAction(nameof(GetMyCatalogue), result);
    }

    /// <summary>
    /// Updates the unit price, lead time, and MOQ for an existing catalogue item.
    /// Supplier cannot edit core product details (name, SKU, etc.).
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateItem(Guid id, [FromBody] SupplierUpdateCatalogueItemRequest request)
    {
        await _service.UpdateCatalogueItemAsync(GetSupplierId(), id, request);
        return Ok(new { message = "Catalogue item updated successfully." });
    }

    /// <summary>
    /// Deactivates a product from the supplier's catalogue (soft delete).
    /// The product remains in the master catalogue.
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeactivateItem(Guid id)
    {
        await _service.DeactivateCatalogueItemAsync(GetSupplierId(), id);
        return Ok(new { message = "Catalogue item deactivated." });
    }
}
