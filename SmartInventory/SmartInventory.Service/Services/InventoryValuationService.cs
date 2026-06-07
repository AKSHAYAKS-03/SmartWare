using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Service.Services;

public class InventoryValuationService : IInventoryValuationService
{
    private readonly IUnitOfWork _uow;

    public InventoryValuationService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task RecalculateWacAsync(Guid productId, int newQuantityReceived, decimal newUnitPrice)
    {
        if (newQuantityReceived <= 0) return;

        var product = await _uow.Repository<Product>().GetByIdAsync(productId);
        if (product == null) return;

        // Calculate total physical stock globally (or per warehouse based on policy, assuming global here)
        var totalPhysicalStock = await _uow.Repository<StockLevel>()
            .Query()
            .Where(sl => sl.ProductId == product.Id)
            .SumAsync(sl => sl.QuantityOnHand);

        decimal oldTotalValue = totalPhysicalStock * product.CostPrice;
        decimal newReceiptValue = newQuantityReceived * newUnitPrice;
        int newTotalQty = totalPhysicalStock + newQuantityReceived;

        if (newTotalQty > 0)
        {
            product.CostPrice = Math.Round((oldTotalValue + newReceiptValue) / newTotalQty, 2);
            product.UpdatedAt = DateTime.UtcNow;
            
            _uow.Repository<Product>().Update(product);
        }
    }
}
