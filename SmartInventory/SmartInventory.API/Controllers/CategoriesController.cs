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
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService) =>
        _categoryService = categoryService;

    [HttpGet]
    public async Task<IActionResult> GetCategories([FromQuery] QueryParameters queryParams) =>
        Ok(await _categoryService.GetCategoriesAsync(queryParams));

    [HttpGet("tree")]
    public async Task<IActionResult> GetCategoryTree() =>
        Ok(await _categoryService.GetCategoryTreeAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCategory(Guid id) =>
        Ok(await _categoryService.GetCategoryByIdAsync(id));

    [EnableRateLimiting("mutations")]

    [HttpPost]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryCreateDto dto)
    {
        var result = await _categoryService.CreateCategoryAsync(dto);
        return CreatedAtAction(nameof(GetCategory), new { id = result.Id }, result);
    }

    [EnableRateLimiting("mutations")]

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] CategoryUpdateDto dto) =>
        Ok(await _categoryService.UpdateCategoryAsync(id, dto));

    [EnableRateLimiting("mutations")]

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        await _categoryService.DeleteCategoryAsync(id);
        return NoContent();
    }
}
