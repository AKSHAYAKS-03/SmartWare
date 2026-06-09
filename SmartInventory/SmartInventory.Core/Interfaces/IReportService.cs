using SmartInventory.Core.DTOs;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Interfaces;

public interface IReportService
{
    Task<IEnumerable<InventoryValuationDto>> GetInventoryValuationReportAsync(
        Guid? warehouseId, ValuationMethod method = ValuationMethod.WeightedAverage);

    Task<IEnumerable<StockMovementTrendDto>> GetStockMovementReportAsync(
        Guid? warehouseId, Guid? productId, DateTime? from, DateTime? to);

    Task<IEnumerable<DeadStockDto>> GetDeadStockReportAsync(
        Guid? warehouseId, int daysThreshold = 90);

    Task<IEnumerable<StockAdjustmentResponseDto>> GetShrinkageReportAsync(
        Guid? warehouseId, DateTime? from, DateTime? to);

    Task<IEnumerable<SupplierPerformanceDto>> GetSupplierPerformanceReportAsync(
        Guid? supplierId = null, Guid? warehouseId = null);

    Task<IEnumerable<PurchaseOrderResponseDto>> GetPoFulfillmentReportAsync(
        Guid? warehouseId, DateTime? from, DateTime? to);

    Task<PagedResult<AuditLogResponseDto>> GetAuditLogAsync(AuditLogQueryParameters queryParams);

    Task<IEnumerable<WarehouseUtilizationDto>> GetWarehouseUtilizationAsync(Guid warehouseId);

    Task<IEnumerable<OverrideAuditReportDto>> GetOverrideAuditReportAsync(Guid? warehouseId, DateTime? from, DateTime? to);

    Task<IEnumerable<TransferVarianceReportDto>> GetTransferVarianceReportAsync(
        Guid? warehouseId, DateTime? from, DateTime? to, AdjustmentStatus? adjustmentStatus = null);

    Task<TransferVarianceSummaryDto> GetTransferVarianceSummaryAsync(
        Guid? warehouseId, DateTime? from, DateTime? to);

    Task<byte[]> ExportToCsvAsync<T>(IEnumerable<T> data);
}
