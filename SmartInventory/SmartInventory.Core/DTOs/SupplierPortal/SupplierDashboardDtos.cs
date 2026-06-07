namespace SmartInventory.Core.DTOs.SupplierPortal;

// ──────────────────────────────────────────────────────────────────────────────
// RESPONSE DTOs
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Top-level summary dashboard for the supplier portal home page.</summary>
public record SupplierDashboardSummaryDto(
    int TotalOrders,
    int PendingOrders,
    int DispatchedOrders,
    int CompletedOrders,
    decimal TotalVolumeSupplied,
    decimal OverallRating,
    double OnTimeDeliveryPercentage,
    double AverageFillRate,
    List<SupplierFillRateHistoryDto> FillRateHistory
);

/// <summary>Fill rate per individual purchase order for chart rendering.</summary>
public record SupplierFillRateHistoryDto(
    string PoNumber,
    DateTime OrderDate,
    double FillRate,
    int PromisedDays,
    int ActualDays,
    bool OnTime
);
