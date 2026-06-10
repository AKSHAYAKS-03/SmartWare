using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Service.Services;

public class TransferVarianceResolver : ITransferVarianceResolver
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notificationService;

    public TransferVarianceResolver(IUnitOfWork uow, INotificationService notificationService)
    {
        _uow = uow;
        _notificationService = notificationService;
    }

    public async Task NotifyVarianceCreatedAsync(
        Guid transferId, Guid adjustmentId, int varianceQty,
        string transferNumber, Guid destinationWarehouseId)
    {
        var destWh = await _uow.Repository<Warehouse>().GetByIdAsync(destinationWarehouseId);
        if (destWh?.ManagerId != null)
        {
            await _notificationService.SendNotificationAsync(
                destWh.ManagerId.Value, NotificationChannel.InApp,
                "TransferVariancePending",
                "Transfer Variance Requires Approval",
                $"Transfer {transferNumber} was received with a shortage of {varianceQty} units. Adjustment approval is required.",
                "WarehouseTransfer", transferId);
        }
    }

    public async Task TryResolveTransferVarianceAsync(Guid transferId)
    {
        var transfer = await _uow.Repository<WarehouseTransfer>()
            .Query()
            .Include(t => t.RequestedByUser)
            .FirstOrDefaultAsync(t => t.Id == transferId);

        if (transfer == null || transfer.Status != TransferStatus.ReceivedWithVariance)
            return;

        var adjustments = await _uow.Repository<StockAdjustment>()
            .Query()
            .Where(a => a.ReferenceType == ReferenceType.Transfer
                     && a.ReferenceId == transferId
                     && a.Reason == AdjustmentReason.LossInTransit)
            .ToListAsync();

        if (adjustments.Count == 0)
            return;

        if (adjustments.Any(a => a.Status == AdjustmentStatus.Pending))
        {
            transfer.VarianceResolutionStatus = TransferVarianceResolutionStatus.PendingApproval;
            _uow.Repository<WarehouseTransfer>().Update(transfer);
            await _uow.CommitAsync();
            return;
        }

        bool anyRejected = adjustments.Any(a => a.Status == AdjustmentStatus.Rejected);
        transfer.VarianceResolutionStatus = anyRejected
            ? TransferVarianceResolutionStatus.Rejected
            : TransferVarianceResolutionStatus.Resolved;
        transfer.VarianceResolvedAt = DateTime.UtcNow;

        if (!anyRejected)
            transfer.Status = TransferStatus.Received;

        _uow.Repository<WarehouseTransfer>().Update(transfer);
        await _uow.CommitAsync();

        string title = anyRejected
            ? "Transfer Variance Rejected"
            : "Transfer Variance Resolved";
        string message = anyRejected
            ? $"Transit variance adjustments for transfer {transfer.TransferNumber} were rejected. Investigation may be required."
            : $"All transit variance adjustments for transfer {transfer.TransferNumber} have been approved.";

        await _notificationService.SendNotificationAsync(
            transfer.RequestedBy, NotificationChannel.InApp,
            "TransferVarianceResolved", title, message,
            "WarehouseTransfer", transferId);
    }
}
