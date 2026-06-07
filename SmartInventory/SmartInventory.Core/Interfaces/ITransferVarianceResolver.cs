namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Handles transfer transit variance lifecycle: notifications and status resolution.
/// </summary>
public interface ITransferVarianceResolver
{
    Task NotifyVarianceCreatedAsync(Guid transferId, Guid adjustmentId, int varianceQty, string transferNumber, Guid destinationWarehouseId);
    Task TryResolveTransferVarianceAsync(Guid transferId);
}
