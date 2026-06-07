using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Core.Interfaces;
using System.Security.Claims;

namespace SmartInventory.API.Controllers;

/// <summary>
/// Supplier portal performance dashboard endpoint.
/// Route prefix: /api/supplier/dashboard
///
/// GET /api/supplier/dashboard — Get performance summary [Supplier]
/// </summary>
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

    /// <summary>
    /// Returns the supplier's own performance dashboard.
    /// Includes: total orders, pending/dispatched/completed counts,
    /// total volume, on-time delivery %, average fill rate, and fill rate history chart data.
    /// 
    /// No cross-supplier comparison data is included.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _service.GetDashboardAsync(GetSupplierId());
        return Ok(result);
    }
}
