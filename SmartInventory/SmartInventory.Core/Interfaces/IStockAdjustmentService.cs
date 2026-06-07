using SmartInventory.Core.DTOs;

namespace SmartInventory.Core.Interfaces;

public interface IStockAdjustmentService
{
    /// <summary>
    /// Initiates a stock correction. If variance is >5% or >$100, throws ApprovalRequiredException and saves as Pending.
    /// </summary>
    Task<StockAdjustmentResponseDto> CreateAdjustmentAsync(StockAdjustmentCreateDto dto);

    /// <summary>
    /// Processes a manager approval or rejection of a pending stock adjustment.
    /// </summary>
    Task<StockAdjustmentResponseDto> ApproveAdjustmentAsync(Guid adjustmentId, StockAdjustmentApprovalDto dto);

    /// <summary>
    /// Returns a paginated, filterable list of all stock adjustments.
    /// </summary>
    Task<PagedResult<StockAdjustmentResponseDto>> GetAdjustmentsAsync(StockAdjustmentQueryParameters queryParams);

    /// <summary>
    /// Returns a single stock adjustment by ID.
    /// </summary>
    Task<StockAdjustmentResponseDto> GetAdjustmentByIdAsync(Guid adjustmentId);

    /// <summary>
    /// Reverses an approved stock adjustment, fixing manual errors.
    /// </summary>
    Task<bool> CancelStockAdjustmentAsync(Guid adjustmentId, Guid performedBy);
}

