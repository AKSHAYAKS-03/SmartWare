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
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IStockLevelService _stockLevelService;
    private readonly ICurrentUserService _currentUser;

    public ProductsController(IProductService productService, IStockLevelService stockLevelService, ICurrentUserService currentUser)
    {
        _productService = productService;
        _stockLevelService = stockLevelService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] ProductQueryParameters queryParams)
    {
        var result = await _productService.GetProductsAsync(queryParams);
        return Ok(result);
    }

    [HttpPost("search")]
    [EnableRateLimiting("reports")] // Prevent expensive query abuse
    public async Task<IActionResult> SearchProducts([FromBody] DynamicQueryRequest request)
    {
        var result = await _productService.SearchProductsAsync(request);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProduct(Guid id)
    {
        var result = await _productService.GetProductByIdAsync(id);
        return Ok(result);
    }

    [EnableRateLimiting("mutations")]

    [HttpPost]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> CreateProduct([FromBody] ProductCreateDto dto)
    {
        var result = await _productService.CreateProductAsync(dto);
        return CreatedAtAction(nameof(GetProduct), new { id = result.Id }, result);
    }

    [EnableRateLimiting("mutations")]

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] ProductUpdateDto dto)
    {
        var result = await _productService.UpdateProductAsync(id, dto);
        return Ok(result);
    }

    [EnableRateLimiting("mutations")]

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        await _productService.DeleteProductAsync(id);
        return NoContent();
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStockProducts([FromQuery] Guid? warehouseId)
    {
        var result = await _productService.GetLowStockProductsAsync(warehouseId ?? _currentUser.WarehouseId);
        return Ok(result);
    }


    [HttpGet("{id:guid}/eoq")]
    public async Task<IActionResult> GetEoq(Guid id, [FromQuery] Guid? warehouseId)
    {
        var wId = warehouseId ?? _currentUser.WarehouseId;
        if (!wId.HasValue) return BadRequest(new { message = "WarehouseId is required." });
        var eoq = await _stockLevelService.CalculateEoqAsync(id, wId.Value);
        return Ok(new { productId = id, warehouseId = wId, eoq });
    }

    [HttpGet("abc-classification")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<IActionResult> GetAbcClassification([FromQuery] Guid? warehouseId)
    {
        var result = await _stockLevelService.GetAbcClassificationAsync(warehouseId ?? _currentUser.WarehouseId);
        return Ok(result);
    }

    [EnableRateLimiting("mutations")]

    [HttpPost("{warehouseId:guid}/update-abc")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> UpdateAbcCategories(Guid warehouseId)
    {
        await _productService.UpdateAbcCategoriesAsync(warehouseId);
        return NoContent();
    }
}
