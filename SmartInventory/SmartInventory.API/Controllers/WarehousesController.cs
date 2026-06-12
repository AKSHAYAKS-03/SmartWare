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
public class WarehousesController : ControllerBase
{
    private readonly IWarehouseService _warehouseService;

    public WarehousesController(IWarehouseService warehouseService) =>
        _warehouseService = warehouseService;


    // ─────────────── Warehouse ──────────────

    [HttpGet]
    public async Task<IActionResult> GetWarehouses([FromQuery] QueryParameters queryParams) =>
        Ok(await _warehouseService.GetWarehousesAsync(queryParams));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetWarehouse(Guid id) =>
        Ok(await _warehouseService.GetWarehouseByIdAsync(id));

    [EnableRateLimiting("mutations")]

    [HttpPost]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> CreateWarehouse([FromBody] WarehouseCreateDto dto)
    {
        var result = await _warehouseService.CreateWarehouseAsync(dto);
        return CreatedAtAction(nameof(GetWarehouse), new { id = result.Id }, result);
    }

    [EnableRateLimiting("mutations")]

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> UpdateWarehouse(Guid id, [FromBody] WarehouseUpdateDto dto) =>
        Ok(await _warehouseService.UpdateWarehouseAsync(id, dto));

    [EnableRateLimiting("mutations")]

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeleteWarehouse(Guid id)
    {
        await _warehouseService.DeleteWarehouseAsync(id);
        return NoContent();
    }

    // ────────────────────Zones ─────────────────────


    [HttpGet("{id:guid}/zones")]
    public async Task<IActionResult> GetZones(Guid id) =>
        Ok(await _warehouseService.GetZonesByWarehouseAsync(id));

    [EnableRateLimiting("mutations")]

    [HttpPost("{id:guid}/zones")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> CreateZone(Guid id, [FromBody] ZoneCreateDto dto)
    {
        dto.WarehouseId = id;
        return Ok(await _warehouseService.CreateZoneAsync(dto));
    }

    [EnableRateLimiting("mutations")]

    [HttpPut("zones/{zoneId:guid}")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> UpdateZone(Guid zoneId, [FromBody] ZoneUpdateDto dto) =>
        Ok(await _warehouseService.UpdateZoneAsync(zoneId, dto));

    [EnableRateLimiting("mutations")]

    [HttpDelete("zones/{zoneId:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeleteZone(Guid zoneId)
    {
        await _warehouseService.DeleteZoneAsync(zoneId);
        return NoContent();
    }

    // ────────────────────Bin Locations─────────────────────


    [HttpGet("zones/{zoneId:guid}/bins")]
    public async Task<IActionResult> GetBins(Guid zoneId) =>
        Ok(await _warehouseService.GetBinsByZoneAsync(zoneId));

    [EnableRateLimiting("mutations")]

    [HttpPost("zones/{zoneId:guid}/bins")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> CreateBin(Guid zoneId, [FromBody] BinLocationCreateDto dto)
    {
        dto.ZoneId = zoneId;
        return Ok(await _warehouseService.CreateBinLocationAsync(dto));
    }

    [EnableRateLimiting("mutations")]

    [HttpPut("bins/{binId:guid}")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> UpdateBin(Guid binId, [FromBody] BinLocationUpdateDto dto) =>
        Ok(await _warehouseService.UpdateBinLocationAsync(binId, dto));

    [EnableRateLimiting("mutations")]

    [HttpDelete("bins/{binId:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeleteBin(Guid binId)
    {
        await _warehouseService.DeleteBinLocationAsync(binId);
        return NoContent();
    }

    // ────────────────────User Access─────────────────────

    [HttpGet("{id:guid}/users")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> GetWarehouseUsers(Guid id) =>
        Ok(await _warehouseService.GetWarehouseUsersAsync(id));

    [EnableRateLimiting("mutations")]

    [HttpPost("{id:guid}/users")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> AssignUserAccess(Guid id, [FromBody] UserWarehouseAccessCreateDto dto)
    {
        dto.WarehouseId = id;
        return Ok(await _warehouseService.AssignUserAccessAsync(dto));
    }

    [EnableRateLimiting("mutations")]

    [HttpDelete("access/{accessId:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> RevokeUserAccess(Guid accessId)
    {
        await _warehouseService.RevokeUserAccessAsync(accessId);
        return NoContent();
    }

    // ──────────────────Putaway Guidance ──────────────────

    [HttpGet("{id:guid}/putaway-suggestion")]
    public async Task<IActionResult> GetPutawaySuggestion(Guid id, [FromQuery] Guid productId)
    {
        var suggestion = await _warehouseService.GetPutawaySuggestionAsync(productId, id);
        if (suggestion == null) return NotFound(new { message = "No available bin locations found." });
        return Ok(suggestion);
    }

    // ───────────────────Capacity Summary──────────────────────

    [HttpGet("{id:guid}/capacity")]
    public async Task<IActionResult> GetWarehouseCapacity(Guid id)
    {
        return Ok(await _warehouseService.GetWarehouseCapacitySummaryAsync(id));
    }

    [HttpGet("zones/{zoneId:guid}/capacity")]
    public async Task<IActionResult> GetZoneCapacity(Guid zoneId)
    {
        return Ok(await _warehouseService.GetZoneCapacitySummaryAsync(zoneId));
    }
}
