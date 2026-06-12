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


    [HttpGet]
    public async Task<IActionResult> GetMyCatalogue()
    {
        var result = await _service.GetMyCatalogueAsync(GetSupplierId());
        return Ok(result);
    }

    [EnableRateLimiting("mutations")]
    [HttpPost]
    public async Task<IActionResult> AddItem([FromBody] SupplierAddCatalogueItemRequest request)
    {
        var result = await _service.AddCatalogueItemAsync(GetSupplierId(), request);
        return CreatedAtAction(nameof(GetMyCatalogue), result);
    }

    [EnableRateLimiting("mutations")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateItem(Guid id, [FromBody] SupplierUpdateCatalogueItemRequest request)
    {
        await _service.UpdateCatalogueItemAsync(GetSupplierId(), id, request);
        return Ok(new { message = "Catalogue item updated successfully." });
    }


    [EnableRateLimiting("mutations")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeactivateItem(Guid id)
    {
        await _service.DeactivateCatalogueItemAsync(GetSupplierId(), id);
        return Ok(new { message = "Catalogue item deactivated." });
    }
}
