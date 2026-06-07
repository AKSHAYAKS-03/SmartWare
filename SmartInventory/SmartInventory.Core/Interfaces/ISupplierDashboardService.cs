using SmartInventory.Core.DTOs.SupplierPortal;

namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Performance dashboard for a supplier.
/// Returns only this supplier's own metrics — no cross-supplier comparisons.
/// </summary>
public interface ISupplierDashboardService
{
    /// <summary>Returns the full performance dashboard summary for the given supplier.</summary>
    Task<SupplierDashboardSummaryDto> GetDashboardAsync(Guid supplierId);
}
