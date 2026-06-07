using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;
using Microsoft.AspNetCore.RateLimiting;

namespace SmartInventory.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
[EnableRateLimiting("reports")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly ICurrentUserService _currentUser;

    public ReportsController(IReportService reportService, ICurrentUserService currentUser)
    {
        _reportService = reportService;
        _currentUser = currentUser;
    }

    private Guid? ResolveWarehouseId(Guid? requestWarehouseId)
    {
        // If user is scoped to a specific warehouse (Manager, Staff, Scoped Viewer), return their scope.
        // Otherwise, fall back to whatever warehouseId filter was passed in the request (Unscoped Admin/Viewer).
        return _currentUser.WarehouseId ?? requestWarehouseId;
    }

    [HttpGet("inventory-valuation")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<IActionResult> GetInventoryValuation(
        [FromQuery] Guid? warehouseId,
        [FromQuery] ValuationMethod method = ValuationMethod.WeightedAverage,
        [FromQuery] bool export = false)
    {
        var wId = ResolveWarehouseId(warehouseId);
        var data = await _reportService.GetInventoryValuationReportAsync(wId, method);
        if (export)
        {
            var csv = await _reportService.ExportToCsvAsync(data);
            return File(csv, "text/csv", $"inventory-valuation-{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        return Ok(data);
    }

    [HttpGet("stock-movements")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<IActionResult> GetStockMovements(
        [FromQuery] Guid? warehouseId, [FromQuery] Guid? productId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] bool export = false)
    {
        var wId = ResolveWarehouseId(warehouseId);
        var data = await _reportService.GetStockMovementReportAsync(wId, productId, from, to);
        if (export)
        {
            var csv = await _reportService.ExportToCsvAsync(data);
            return File(csv, "text/csv", $"stock-movements-{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        return Ok(data);
    }

    [HttpGet("dead-stock")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<IActionResult> GetDeadStock(
        [FromQuery] Guid? warehouseId, [FromQuery] int daysThreshold = 90, [FromQuery] bool export = false)
    {
        var wId = ResolveWarehouseId(warehouseId);
        var data = await _reportService.GetDeadStockReportAsync(wId, daysThreshold);
        if (export)
        {
            var csv = await _reportService.ExportToCsvAsync(data);
            return File(csv, "text/csv", $"dead-stock-{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        return Ok(data);
    }

    [HttpGet("shrinkage")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<IActionResult> GetShrinkage(
        [FromQuery] Guid? warehouseId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] bool export = false)
    {
        var wId = ResolveWarehouseId(warehouseId);
        var data = await _reportService.GetShrinkageReportAsync(wId, from, to);
        if (export)
        {
            var csv = await _reportService.ExportToCsvAsync(data);
            return File(csv, "text/csv", $"shrinkage-{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        return Ok(data);
    }

    [HttpGet("supplier-performance")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<IActionResult> GetSupplierPerformance(
        [FromQuery] Guid? supplierId, [FromQuery] Guid? warehouseId, [FromQuery] bool export = false)
    {
        var wId = ResolveWarehouseId(warehouseId);
        var data = await _reportService.GetSupplierPerformanceReportAsync(supplierId, wId);
        if (export)
        {
            var csv = await _reportService.ExportToCsvAsync(data);
            return File(csv, "text/csv", $"supplier-performance-{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        return Ok(data);
    }

    [HttpGet("po-fulfillment")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<IActionResult> GetPoFulfillment(
        [FromQuery] Guid? warehouseId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] bool export = false)
    {
        var wId = ResolveWarehouseId(warehouseId);
        var data = await _reportService.GetPoFulfillmentReportAsync(wId, from, to);
        if (export)
        {
            var csv = await _reportService.ExportToCsvAsync(data);
            return File(csv, "text/csv", $"po-fulfillment-{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        return Ok(data);
    }

    [HttpGet("audit-log")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> GetAuditLog([FromQuery] AuditLogQueryParameters queryParams) =>
        Ok(await _reportService.GetAuditLogAsync(queryParams));

    [HttpGet("warehouse-utilization")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<IActionResult> GetWarehouseUtilization(
        [FromQuery] Guid warehouseId,
        [FromQuery] bool export = false)
    {
        var wId = ResolveWarehouseId(warehouseId);
        if (wId == null) return BadRequest("WarehouseId is required for this report.");
        
        var data = await _reportService.GetWarehouseUtilizationAsync(wId.Value);
        if (export)
        {
            var csv = await _reportService.ExportToCsvAsync(data);
            return File(csv, "text/csv", $"warehouse-utilization-{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        return Ok(data);
    }

    [HttpGet("transfer-variance")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<IActionResult> GetTransferVariance(
        [FromQuery] Guid? warehouseId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] AdjustmentStatus? adjustmentStatus, [FromQuery] bool export = false)
    {
        var wId = ResolveWarehouseId(warehouseId);
        var data = await _reportService.GetTransferVarianceReportAsync(wId, from, to, adjustmentStatus);
        if (export)
        {
            var csv = await _reportService.ExportToCsvAsync(data);
            return File(csv, "text/csv", $"transfer-variance-{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        return Ok(data);
    }

    [HttpGet("transfer-variance/summary")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<IActionResult> GetTransferVarianceSummary(
        [FromQuery] Guid? warehouseId, [FromQuery] DateTime? from, [FromQuery] DateTime? to) =>
        Ok(await _reportService.GetTransferVarianceSummaryAsync(ResolveWarehouseId(warehouseId), from, to));

    [HttpGet("override-audit")]
    [Authorize(Policy = "RequireAdmin")] // Restrict to admin
    public async Task<IActionResult> GetOverrideAuditReport(
        [FromQuery] Guid? warehouseId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] bool export = false)
    {
        var wId = ResolveWarehouseId(warehouseId);
        var data = await _reportService.GetOverrideAuditReportAsync(wId, from, to);
        if (export)
        {
            var csv = await _reportService.ExportToCsvAsync(data);
            return File(csv, "text/csv", $"override-audit-{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        return Ok(data);
    }
}
