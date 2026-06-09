using SmartInventory.Core.DTOs;

namespace SmartInventory.Core.Interfaces;

public interface IStockAdjustmentService
{
    // Initiates a stock correction. If variance is >5% or >$100, throws ApprovalRequiredException and saves as Pending.
    Task<StockAdjustmentResponseDto> CreateAdjustmentAsync(StockAdjustmentCreateDto dto);

    // Processes a manager approval or rejection of a pending stock adjustment.
    Task<StockAdjustmentResponseDto> ApproveAdjustmentAsync(Guid adjustmentId, StockAdjustmentApprovalDto dto);

    // Returns a paginated, filterable list of all stock adjustments.
    Task<PagedResult<StockAdjustmentResponseDto>> GetAdjustmentsAsync(StockAdjustmentQueryParameters queryParams);

    // Returns a single stock adjustment by ID.
    Task<StockAdjustmentResponseDto> GetAdjustmentByIdAsync(Guid adjustmentId);

    // Reverses an approved stock adjustment, fixing manual errors.
    Task<bool> CancelStockAdjustmentAsync(Guid adjustmentId, Guid performedBy);
}

