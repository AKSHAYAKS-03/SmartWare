using SmartInventory.Core.DTOs.SupplierPortal;

namespace SmartInventory.Core.Interfaces;
public interface ISupplierDashboardService
{
    Task<SupplierDashboardSummaryDto> GetDashboardAsync(Guid supplierId);
}
