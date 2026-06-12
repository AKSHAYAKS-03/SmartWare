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
[Authorize] 
public class LookupController : ControllerBase
{
    private readonly IMasterDataService _masterDataService;

    public LookupController(IMasterDataService masterDataService)
    {
        _masterDataService = masterDataService;
    }

    [HttpGet("master-data")]
    [EnableRateLimiting("reports")] 
    [ProducesResponseType(typeof(MasterDataResponseDto), 200)]
    public async Task<IActionResult> GetMasterData()
    {
        var data = await _masterDataService.GetMasterDataAsync();
        return Ok(data);
    }
}
