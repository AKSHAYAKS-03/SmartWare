using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Core.Interfaces;
using System.Security.Claims;

namespace SmartInventory.API.Controllers;


[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/supplier/dashboard")]
[Authorize(Policy = "RequireSupplier")]
public class SupplierDashboardController : ControllerBase
{
    private readonly ISupplierDashboardService _service;

    public SupplierDashboardController(ISupplierDashboardService service)
    {
        _service = service;
    }

    private Guid GetSupplierId() => Guid.Parse(User.FindFirstValue("supplierId")!);

    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _service.GetDashboardAsync(GetSupplierId());
        return Ok(result);
    }
}
