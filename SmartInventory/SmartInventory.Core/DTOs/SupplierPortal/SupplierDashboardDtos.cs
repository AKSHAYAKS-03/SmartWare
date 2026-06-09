namespace SmartInventory.Core.DTOs.SupplierPortal;

// RESPONSE DTOs

//Top-level summary dashboard for the supplier portal home page
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

//Fill rate per individual purchase order for chart rendering
public record SupplierFillRateHistoryDto(
    string PoNumber,
    DateTime OrderDate,
    double FillRate,
    int PromisedDays,
    int ActualDays,
    bool OnTime
);
