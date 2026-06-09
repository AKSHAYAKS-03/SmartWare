using SmartInventory.Core.DTOs;

namespace SmartInventory.Core.Interfaces;

public interface IPurchaseOrderService
{
    Task<PurchaseOrderResponseDto> CreatePurchaseOrderAsync(PurchaseOrderCreateDto dto);
    Task<PurchaseOrderResponseDto> UpdatePurchaseOrderAsync(Guid poId, PurchaseOrderUpdateDto dto);
    Task<PurchaseOrderResponseDto> SubmitForApprovalAsync(Guid poId);
    Task<PurchaseOrderResponseDto> ApprovePurchaseOrderAsync(Guid poId, PurchaseOrderApprovalDto dto);
    Task<GoodsReceiptResponseDto> ReceiveGoodsAsync(GoodsReceiptCreateDto dto);
    Task<GoodsReceiptResponseDto> ReceiveGoodsByBarcodeAsync(BarcodeGoodsReceiptCreateDto dto);
    Task<PurchaseOrderResponseDto> GetPurchaseOrderByIdAsync(Guid poId);
    Task<PagedResult<PurchaseOrderResponseDto>> GetPurchaseOrdersAsync(PurchaseOrderQueryParameters queryParams);
    Task<PagedResult<PurchaseOrderResponseDto>> SearchPurchaseOrdersAsync(DynamicQueryRequest request);
    Task<IEnumerable<GoodsReceiptResponseDto>> GetGoodsReceiptsAsync(Guid poId);
    Task<bool> CancelGoodsReceiptAsync(Guid receiptId, Guid performedBy);
    Task<PurchaseOrderResponseDto> CancelPurchaseOrderAsync(Guid poId, Guid performedBy);
}
