using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Service.Services;


public class ReportService : IReportService
{
    private readonly IUnitOfWork _uow;
    private readonly IStockLevelService _stockLevelService;

    public ReportService(IUnitOfWork uow, IStockLevelService stockLevelService)
    {
        _uow = uow;
        _stockLevelService = stockLevelService;
    }


    // Inventory Valuation 

    public async Task<IEnumerable<InventoryValuationDto>> GetInventoryValuationReportAsync(
        Guid? warehouseId, ValuationMethod method = ValuationMethod.WeightedAverage)
    {
        IQueryable<StockLevel> stockQuery = _uow.Repository<StockLevel>().Query()
            .Include(sl => sl.Product).ThenInclude(p => p.Category);

        if (warehouseId.HasValue)
            stockQuery = stockQuery.Where(sl => sl.WarehouseId == warehouseId.Value);

        var stockLevels = await stockQuery.ToListAsync();

        var results = stockLevels
            .GroupBy(sl => new { sl.ProductId, sl.Product, sl.Product.Category })
            .Select(g => new InventoryValuationDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.Product.Name,
                ProductSKU = g.Key.Product.SKU,
                CategoryName = g.Key.Category?.Name ?? "Uncategorized",
                TotalStock = g.Sum(sl => sl.QuantityOnHand),
                UnitCost = g.Key.Product.CostPrice
            })
            .Where(r => r.TotalStock > 0)
            .OrderByDescending(r => r.TotalValue)
            .ToList();

        return results;
    }

    // Stock Movement Trend 

    public async Task<IEnumerable<StockMovementTrendDto>> GetStockMovementReportAsync(
        Guid? warehouseId, Guid? productId, DateTime? from, DateTime? to)
    {
        var query = _uow.Repository<StockMovement>().Query();

        if (warehouseId.HasValue) query = query.Where(m => m.WarehouseId == warehouseId.Value);
        if (productId.HasValue) query = query.Where(m => m.ProductId == productId.Value);
        if (from.HasValue) query = query.Where(m => m.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(m => m.CreatedAt <= to.Value);

        var movements = await query
            .GroupBy(m => new { Date = m.CreatedAt.Date, m.MovementType })
            .Select(g => new StockMovementTrendDto
            {
                Date = g.Key.Date,

                MovementType = g.Key.MovementType.ToString(),
                TotalQuantity = g.Sum(m => m.Quantity),
                TransactionCount = g.Count()
            })
            .OrderBy(r => r.Date)
            .ToListAsync();

        return movements;
    }

    //  Dead Stock 

    public async Task<IEnumerable<DeadStockDto>> GetDeadStockReportAsync(
        Guid? warehouseId, int daysThreshold = 90)
    {
        var cutoff = DateTime.UtcNow.AddDays(-daysThreshold);

        var query = await _uow.Repository<Product>().Query()
            .Select(p => new
            {
                Product = p,
                CategoryName = p.Category != null ? p.Category.Name : "Uncategorized",
                TotalQty = p.StockLevels
                    .Where(sl => warehouseId == null || sl.WarehouseId == warehouseId)
                    .Sum(sl => sl.QuantityOnHand),
                LastMoved = _uow.Repository<StockMovement>().Query()
                    .Where(m => m.ProductId == p.Id && (warehouseId == null || m.WarehouseId == warehouseId))
                    .Max(m => (DateTime?)m.CreatedAt)
            })
            .Where(x => x.TotalQty > 0 && (x.LastMoved == null || x.LastMoved < cutoff))
            .ToListAsync();

        return query.Select(x => new DeadStockDto
        {
            ProductId = x.Product.Id,
            ProductName = x.Product.Name,
            ProductSKU = x.Product.SKU,
            CategoryName = x.CategoryName,
            QuantityOnHand = x.TotalQty,
            LastMovementDate = x.LastMoved,
            DaysSinceLastMovement = x.LastMoved.HasValue 
                ? (int)(DateTime.UtcNow - x.LastMoved.Value).TotalDays 
                : -1
        }).OrderByDescending(r => r.DaysSinceLastMovement).ToList();
    }

    //  Shrinkage Report

    public async Task<IEnumerable<StockAdjustmentResponseDto>> GetShrinkageReportAsync(
        Guid? warehouseId, DateTime? from, DateTime? to)
    {
        var query = _uow.Repository<StockAdjustment>()
            .Query()
            .Include(a => a.Product)
            .Include(a => a.Warehouse)
            .Include(a => a.PerformedByUser)
            .Include(a => a.ApprovedByUser)
            .Where(a => a.ShrinkageReason != null); // Only shrinkage adjustments

        if (warehouseId.HasValue) query = query.Where(a => a.WarehouseId == warehouseId.Value);
        if (from.HasValue) query = query.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(a => a.CreatedAt <= to.Value);

        var data = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();

        return data.Select(a => new StockAdjustmentResponseDto
        {
            Id = a.Id,
            AdjustmentNumber = a.AdjustmentNumber,
            ProductId = a.ProductId,
            ProductName = a.Product.Name,
            ProductSKU = a.Product.SKU,
            WarehouseId = a.WarehouseId,
            WarehouseName = a.Warehouse.Name,
            BinLocationId = a.BinLocationId,
            Reason = a.Reason,
            Status = a.Status,
            QuantityBefore = a.QuantityBefore,
            QuantityAfter = a.QuantityAfter,
            QuantityChange = a.QuantityChange,
            Notes = a.Notes,
            PerformedBy = a.PerformedBy,
            PerformedByUserName = a.PerformedByUser.FullName,
            ApprovedBy = a.ApprovedBy,
            ApprovedByUserName = a.ApprovedByUser?.FullName,
            CreatedAt = a.CreatedAt
        });
    }

    //  Supplier Performance 

    public async Task<IEnumerable<SupplierPerformanceDto>> GetSupplierPerformanceReportAsync(
        Guid? supplierId = null, Guid? warehouseId = null)
    {
        IQueryable<SupplierPerformanceLog> logsQuery = _uow.Repository<SupplierPerformanceLog>()
            .Query()
            .Include(l => l.Supplier)
            .Include(l => l.PurchaseOrder);

        if (supplierId.HasValue)
            logsQuery = logsQuery.Where(l => l.SupplierId == supplierId.Value);
            
        if (warehouseId.HasValue)
            logsQuery = logsQuery.Where(l => l.PurchaseOrder.WarehouseId == warehouseId.Value);

        var logs = await logsQuery.ToListAsync();

        return logs
            .GroupBy(l => new { l.SupplierId, l.Supplier.Name, l.Supplier.Code })
            .Select(g => new SupplierPerformanceDto
            {
                SupplierId = g.Key.SupplierId,
                SupplierName = g.Key.Name,
                SupplierCode = g.Key.Code,
                TotalOrders = g.Count(),
                AverageLeadTimeDays = g.Average(l => l.ActualDays),
                AverageFillRate = g.Average(l => l.FillRate),
                PerformanceRating = g.First().Supplier.Rating
            })
            .OrderByDescending(r => r.PerformanceRating)
            .ToList();
    }

    // PO Fulfillment

    public async Task<IEnumerable<PurchaseOrderResponseDto>> GetPoFulfillmentReportAsync(
        Guid? warehouseId, DateTime? from, DateTime? to)
    {
        var query = _uow.Repository<PurchaseOrder>()
            .Query()
            .Include(po => po.Supplier)
            .Include(po => po.Warehouse)
            .Include(po => po.CreatedByUser)
            .Include(po => po.ApprovedByUser)
            .Include(po => po.Items).ThenInclude(i => i.Product)
            .Where(po => po.Status == PurchaseOrderStatus.Received ||
                         po.Status == PurchaseOrderStatus.Closed ||
                         po.Status == PurchaseOrderStatus.PartiallyReceived);

        if (warehouseId.HasValue) query = query.Where(po => po.WarehouseId == warehouseId.Value);
        if (from.HasValue) query = query.Where(po => po.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(po => po.CreatedAt <= to.Value);

        var data = await query.OrderByDescending(po => po.CreatedAt).ToListAsync();

        return data.Select(po => new PurchaseOrderResponseDto
        {
            Id = po.Id,
            PoNumber = po.PoNumber,
            SupplierId = po.SupplierId,
            SupplierName = po.Supplier.Name,
            WarehouseId = po.WarehouseId,
            WarehouseName = po.Warehouse.Name,
            Status = po.Status,
            TotalAmount = po.TotalAmount,
            ExpectedDelivery = po.ExpectedDelivery,
            ActualDelivery = po.ActualDelivery,
            Notes = po.Notes,
            CreatedBy = po.CreatedBy,
            CreatedByUserName = po.CreatedByUser.FullName,
            ApprovedBy = po.ApprovedBy,
            ApprovedByUserName = po.ApprovedByUser?.FullName,
            CreatedAt = po.CreatedAt,
            Items = po.Items.Select(i => new PurchaseOrderItemResponseDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.Product.Name,
                ProductSKU = i.Product.SKU,
                QuantityOrdered = i.QuantityOrdered,
                QuantityReceived = i.QuantityReceived,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice
            }).ToList()
        });
    }

    // Audit Log Viewer

    public async Task<PagedResult<AuditLogResponseDto>> GetAuditLogAsync(
        AuditLogQueryParameters queryParams)
    {
        var query = _uow.Repository<AuditLog>().Query()
            .Include(a => a.User)
            .AsQueryable();

        if (queryParams.UserId.HasValue)
            query = query.Where(a => a.UserId == queryParams.UserId.Value);

        if (!string.IsNullOrWhiteSpace(queryParams.EntityType))
            query = query.Where(a => a.EntityType == queryParams.EntityType);

        if (!string.IsNullOrWhiteSpace(queryParams.Action))
            query = query.Where(a => a.Action == queryParams.Action);

        if (queryParams.FromDate.HasValue)
            query = query.Where(a => a.CreatedAt >= queryParams.FromDate.Value);

        if (queryParams.ToDate.HasValue)
            query = query.Where(a => a.CreatedAt <= queryParams.ToDate.Value);

        int total = await query.CountAsync();

        var data = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync();

        return new PagedResult<AuditLogResponseDto>
        {
            Data = data.Select(a => new AuditLogResponseDto
            {
                Id = a.Id,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Action = a.Action,
                OldValues = a.OldValues,
                NewValues = a.NewValues,
                IpAddress = a.IpAddress,
                UserId = a.UserId,
                UserFullName = a.User?.FullName ?? "System",
                CreatedAt = a.CreatedAt
            }),
            TotalCount = total,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    // Capacity & Utilization Reports 

    public async Task<IEnumerable<WarehouseUtilizationDto>> GetWarehouseUtilizationAsync(Guid warehouseId)
    {
        var zones = await _uow.Repository<WarehouseZone>()
            .Query()
            .Include(z => z.BinLocations)
            .Where(z => z.WarehouseId == warehouseId)
            .ToListAsync();

        return zones.Select(z => new WarehouseUtilizationDto
        {
            ZoneId = z.Id,
            ZoneName = z.Name,
            TotalVolumeCapacity = z.BinLocations.Sum(b => b.MaxVolumeCm3),
            UtilizedVolume = z.BinLocations.Sum(b => b.UtilizedVolumeCm3)
        }).OrderByDescending(u => u.UtilizationPercentage).ToList();
    }

    public async Task<IEnumerable<OverrideAuditReportDto>> GetOverrideAuditReportAsync(Guid? warehouseId, DateTime? from, DateTime? to)
    {
        var query = _uow.Repository<OverrideAuditLog>()
            .Query()
            .Include(l => l.User)
            .Include(l => l.TargetBin)
                .ThenInclude(b => b!.Zone)
            .Include(l => l.Product)
            .AsQueryable();

        if (warehouseId.HasValue)
        {
            query = query.Where(l => l.TargetBin != null && l.TargetBin.Zone!.WarehouseId == warehouseId.Value);
        }

        if (from.HasValue) query = query.Where(l => l.Timestamp >= from.Value);
        if (to.HasValue) query = query.Where(l => l.Timestamp <= to.Value);

        var data = await query.OrderByDescending(l => l.Timestamp).ToListAsync();

        return data.Select(l => new OverrideAuditReportDto
        {
            Id = l.Id,
            UserId = l.UserId,
            UserName = l.User.FullName,
            Timestamp = l.Timestamp,
            RuleBroken = l.RuleBroken,
            OverrideReason = l.OverrideReason,
            TargetBinId = l.TargetBinId,
            TargetBinCode = l.TargetBin?.Barcode ?? l.TargetBin?.BinCode,
            ProductId = l.ProductId,
            ProductName = l.Product?.Name
        });
    }

    //  Transfer Variance Report

    public async Task<IEnumerable<TransferVarianceReportDto>> GetTransferVarianceReportAsync(
        Guid? warehouseId, DateTime? from, DateTime? to, AdjustmentStatus? adjustmentStatus = null)
    {
        var transferQuery = _uow.Repository<WarehouseTransfer>()
            .Query()
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .Where(t => t.Items.Any(i => i.QuantityDispatched > i.QuantityReceived));

        if (warehouseId.HasValue)
            transferQuery = transferQuery.Where(t =>
                t.FromWarehouseId == warehouseId.Value || t.ToWarehouseId == warehouseId.Value);

        if (from.HasValue)
            transferQuery = transferQuery.Where(t => t.UpdatedAt >= from.Value || t.CreatedAt >= from.Value);

        if (to.HasValue)
            transferQuery = transferQuery.Where(t => t.UpdatedAt <= to.Value || t.CreatedAt <= to.Value);

        var transfers = await transferQuery.OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt).ToListAsync();
        var transferIds = transfers.Select(t => t.Id).ToList();

        var adjustments = await _uow.Repository<StockAdjustment>()
            .Query()
            .Include(a => a.ApprovedByUser)
            .Where(a => a.ReferenceType == ReferenceType.Transfer
                     && a.ReferenceId.HasValue
                     && transferIds.Contains(a.ReferenceId.Value)
                     && a.Reason == AdjustmentReason.LossInTransit)
            .ToListAsync();

        var adjLookup = adjustments.ToLookup(a => (a.ReferenceId!.Value, a.ProductId));

        var results = new List<TransferVarianceReportDto>();
        foreach (var transfer in transfers)
        {
            foreach (var item in transfer.Items.Where(i => i.QuantityDispatched > i.QuantityReceived))
            {
                var variance = item.QuantityDispatched - item.QuantityReceived;
                var adj = adjLookup[(transfer.Id, item.ProductId)].FirstOrDefault();

                if (adjustmentStatus.HasValue && (adj == null || adj.Status != adjustmentStatus.Value))
                    continue;

                results.Add(new TransferVarianceReportDto
                {
                    TransferId = transfer.Id,
                    TransferNumber = transfer.TransferNumber,
                    TransferItemId = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.Product.Name,
                    ProductSKU = item.Product.SKU,
                    FromWarehouseName = transfer.FromWarehouse.Name,
                    ToWarehouseName = transfer.ToWarehouse.Name,
                    QuantityRequested = item.QuantityRequested,
                    QuantityDispatched = item.QuantityDispatched,
                    QuantityReceived = item.QuantityReceived,
                    VarianceQuantity = variance,
                    TransferStatus = transfer.Status,
                    AdjustmentId = adj?.Id,
                    AdjustmentStatus = adj?.Status,
                    VarianceResolutionStatus = transfer.VarianceResolutionStatus,
                    ApprovedByUserName = adj?.ApprovedByUser?.FullName,
                    ApprovedDate = adj?.Status == AdjustmentStatus.Approved ? adj.UpdatedAt : null,
                    EstimatedLossValue = variance * item.Product.CostPrice,
                    ReceivedDate = transfer.UpdatedAt ?? transfer.CreatedAt
                });
            }
        }

        return results;
    }

    public async Task<TransferVarianceSummaryDto> GetTransferVarianceSummaryAsync(
        Guid? warehouseId, DateTime? from, DateTime? to)
    {
        var rows = (await GetTransferVarianceReportAsync(warehouseId, from, to)).ToList();

        return new TransferVarianceSummaryDto
        {
            TotalVariances = rows.Count,
            PendingApproval = rows.Count(r => r.AdjustmentStatus == AdjustmentStatus.Pending),
            Approved = rows.Count(r => r.AdjustmentStatus == AdjustmentStatus.Approved),
            Rejected = rows.Count(r => r.AdjustmentStatus == AdjustmentStatus.Rejected),
            TotalEstimatedLoss = rows.Sum(r => r.EstimatedLossValue),
            PendingLossValue = rows.Where(r => r.AdjustmentStatus == AdjustmentStatus.Pending).Sum(r => r.EstimatedLossValue)
        };
    }

    //  CSV Export


    public Task<byte[]> ExportToCsvAsync<T>(IEnumerable<T> data)
    {
        var sb = new StringBuilder();
        var properties = typeof(T).GetProperties()
            .Where(p => p.CanRead && !p.GetMethod!.IsVirtual)
            .ToArray();

        // Header row
        sb.AppendLine(string.Join(",", properties.Select(p => p.Name)));

        // Data rows
        foreach (var item in data)
        {
            var values = properties.Select(p =>
            {
                var val = p.GetValue(item)?.ToString() ?? string.Empty;
                // Escape commas and quotes inside values
                return val.Contains(',') || val.Contains('"')
                    ? $"\"{val.Replace("\"", "\"\"")}\""
                    : val;
            });
            sb.AppendLine(string.Join(",", values));
        }

        return Task.FromResult(Encoding.UTF8.GetBytes(sb.ToString()));
    }
}
