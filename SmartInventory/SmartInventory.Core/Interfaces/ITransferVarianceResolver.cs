namespace SmartInventory.Core.Interfaces;

public interface ITransferVarianceResolver
{
    Task NotifyVarianceCreatedAsync(Guid transferId, Guid adjustmentId, int varianceQty, string transferNumber, Guid destinationWarehouseId);
    Task TryResolveTransferVarianceAsync(Guid transferId);
}
