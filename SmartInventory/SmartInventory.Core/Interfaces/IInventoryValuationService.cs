namespace SmartInventory.Core.Interfaces;

public interface IInventoryValuationService
{
    
    /// Recalculates the Weighted Average Cost (WAC) for a product.

    Task RecalculateWacAsync(Guid productId, int newQuantityReceived, decimal newUnitPrice);
}
