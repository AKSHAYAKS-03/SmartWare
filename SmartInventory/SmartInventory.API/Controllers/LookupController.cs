using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Interfaces;
using System.Threading.Tasks;
using Asp.Versioning;

namespace SmartInventory.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize] // Require basic authentication to hit lookups
public class LookupController : ControllerBase
{
    private readonly IMasterDataService _masterDataService;

    public LookupController(IMasterDataService masterDataService)
    {
        _masterDataService = masterDataService;
    }

    /// <summary>
    /// Retrieves a unified payload of all static master data for dropdowns (Categories, Warehouses, Roles).
    /// Highly optimized with server-side caching.
    /// </summary>
    [HttpGet("master-data")]
    [EnableRateLimiting("reports")] // Use a standard rate limit to prevent spam
    [ProducesResponseType(typeof(MasterDataResponseDto), 200)]
    public async Task<IActionResult> GetMasterData()
    {
        var data = await _masterDataService.GetMasterDataAsync();
        return Ok(data);
    }
}
