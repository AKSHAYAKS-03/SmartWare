namespace SmartInventory.Core.Interfaces;

public interface IInventoryValuationService
{
    /// <summary>
    /// Recalculates the Weighted Average Cost (WAC) for a product.
    /// This should be called whenever new stock is received at a new cost price.
    /// </summary>
    /// <param name="productId">The ID of the product.</param>
    /// <param name="newQuantityReceived">The amount of new physical stock received.</param>
    /// <param name="newUnitPrice">The unit price of the newly received stock.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task RecalculateWacAsync(Guid productId, int newQuantityReceived, decimal newUnitPrice);
}
