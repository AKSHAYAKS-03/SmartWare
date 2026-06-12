using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Service.Services;


public class SupplierDashboardService : ISupplierDashboardService
{
    private readonly IUnitOfWork _uow;

    public SupplierDashboardService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<SupplierDashboardSummaryDto> GetDashboardAsync(Guid supplierId)
    {
        // Load all POs for this supplier
        var orders = await _uow.Repository<PurchaseOrder>().Query()
            .Where(po => po.SupplierId == supplierId)
            .ToListAsync();

        var totalOrders = orders.Count;
        var pendingOrders = orders.Count(po =>
            po.Status == PurchaseOrderStatus.Submitted ||
            po.Status == PurchaseOrderStatus.Approved);
        var dispatchedOrders = orders.Count(po => po.DispatchedAt.HasValue && po.Status != PurchaseOrderStatus.Received);
        var completedOrders = orders.Count(po => po.Status == PurchaseOrderStatus.Received || po.Status == PurchaseOrderStatus.Closed);
        var totalVolume = orders.Sum(po => po.TotalAmount);

        // Load supplier rating
        var supplier = await _uow.Repository<Supplier>().Query()
            .FirstOrDefaultAsync(s => s.Id == supplierId);
        var overallRating = supplier?.Rating ?? 0m;

        // Load performance logs for on-time delivery and fill rate
        var logs = await _uow.Repository<SupplierPerformanceLog>().Query()
            .Include(l => l.PurchaseOrder)
            .Where(l => l.SupplierId == supplierId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        double onTimePercentage = 0;
        double avgFillRate = 0;

        if (logs.Count > 0)
        {
            onTimePercentage = Math.Round(
                (double)logs.Count(l => l.ActualDays <= l.PromisedDays) / logs.Count * 100, 1);
            avgFillRate = Math.Round(logs.Average(l => (double)l.FillRate) * 100, 1);
        }

        var fillRateHistory = logs.Take(20).Select(l => new SupplierFillRateHistoryDto(
            PoNumber: l.PurchaseOrder?.PoNumber ?? string.Empty,
            OrderDate: l.CreatedAt,
            FillRate: Math.Round((double)l.FillRate * 100, 1),
            PromisedDays: l.PromisedDays,
            ActualDays: l.ActualDays,
            OnTime: l.ActualDays <= l.PromisedDays
        )).ToList();

        return new SupplierDashboardSummaryDto(
            TotalOrders: totalOrders,
            PendingOrders: pendingOrders,
            DispatchedOrders: dispatchedOrders,
            CompletedOrders: completedOrders,
            TotalVolumeSupplied: totalVolume,
            OverallRating: overallRating,
            OnTimeDeliveryPercentage: onTimePercentage,
            AverageFillRate: avgFillRate,
            FillRateHistory: fillRateHistory
        );
    }
}
