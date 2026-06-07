using SmartInventory.Core.DTOs;

namespace SmartInventory.Core.Interfaces;

public interface ITransferService
{
    Task<TransferResponseDto> CreateTransferAsync(TransferCreateDto dto);
    Task<TransferResponseDto> ApproveTransferAsync(Guid transferId, TransferApprovalDto dto);
    Task<TransferResponseDto> DispatchTransferAsync(Guid transferId, Guid performedBy);
    Task<TransferResponseDto> ReceiveTransferAsync(Guid transferId, TransferReceiveDto dto, Guid performedBy);
    Task<TransferResponseDto> GetTransferByIdAsync(Guid transferId, Guid? currentWarehouseId = null);
    Task<PagedResult<TransferResponseDto>> GetTransfersAsync(TransferQueryParameters queryParams);
    Task<PagedResult<TransferResponseDto>> SearchTransfersAsync(DynamicQueryRequest request);
    Task<bool> TransferBinToBinAsync(BinTransferCreateDto dto, Guid performedBy);
}
