using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Service.Services;

public class StockLevelService : IStockLevelService
{
    private readonly IUnitOfWork _uow;

    public StockLevelService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<List<AbcClassificationResultDto>> GetAbcClassificationAsync(Guid? warehouseId = null)
    {
        var stockLevelsQuery = _uow.Repository<StockLevel>().Query();
        
        if (warehouseId.HasValue)
        {
            stockLevelsQuery = stockLevelsQuery.Where(sl => sl.WarehouseId == warehouseId.Value);
        }

        var stockLevels = await stockLevelsQuery
            .Include(sl => sl.Product)
            .ToListAsync();

        // Group by product and compute total value (Selling Price * QuantityOnHand)
        var productGroups = stockLevels
            .GroupBy(sl => sl.ProductId)
            .Select(g =>
            {
                var first = g.First();
                var totalQty = g.Sum(sl => sl.QuantityOnHand);
                var unitPrice = first.Product.SellingPrice;
                var totalVal = totalQty * unitPrice;

                return new AbcClassificationResultDto
                {
                    ProductId = g.Key,
                    ProductName = first.Product.Name,
                    ProductSKU = first.Product.SKU, // Correct uppercase SKU
                    QuantityOnHand = totalQty,
                    UnitPrice = unitPrice,
                    TotalValue = totalVal
                };
            })
            .OrderByDescending(r => r.TotalValue)
            .ToList();

        decimal totalValuation = productGroups.Sum(pg => pg.TotalValue);

        if (totalValuation == 0)
        {
            // If total valuation is zero, classify all as C
            foreach (var item in productGroups)
            {
                item.Class = "C";
            }
            return productGroups;
        }

        decimal runningValue = 0;
        foreach (var item in productGroups)
        {
            runningValue += item.TotalValue;
            item.CumulativeValue = runningValue;
            item.CumulativePercentage = (double)(runningValue / totalValuation) * 100.0;

            if (item.CumulativePercentage <= 70.0)
            {
                item.Class = "A";
            }
            else if (item.CumulativePercentage <= 90.0)
            {
                item.Class = "B";
            }
            else
            {
                item.Class = "C";
            }
        }

        return productGroups;
    }

    public async Task<double> CalculateEoqAsync(Guid productId, Guid warehouseId)
    {
        var product = await _uow.Repository<Product>().GetByIdAsync(productId);
        if (product == null) return 0;

        // 1. Calculate Annual Demand (D) from historical outbound stock movements (Sale, TransferOut, WriteOff)
        var cutoff = DateTime.UtcNow.AddDays(-90);
        var movementsQuery = _uow.Repository<StockMovement>().Query();
        
        var outboundMovements = await movementsQuery
            .Where(m => m.ProductId == productId &&
                        m.WarehouseId == warehouseId &&
                        m.CreatedAt >= cutoff &&
                        (m.MovementType == MovementType.Sale ||
                         m.MovementType == MovementType.TransferOut ||
                         m.MovementType == MovementType.WriteOff))
            .ToListAsync();

        var ninetyDayDemand = outboundMovements.Sum(m => m.Quantity);
        
        // Extrapolate to 365 days (~4 times the 90 day demand)
        double annualDemand = ninetyDayDemand * 4.0;
        if (annualDemand == 0)
        {
            annualDemand = 100.0; // Default fallback to avoid 0 recommendation
        }

        // 2. Setup Cost (S) - pull from SupplierProduct preferred flag or default to 50.0
        double setupCost = 50.0;
        var supplierProductsQuery = _uow.Repository<SupplierProduct>().Query();
        var preferredSupplier = await supplierProductsQuery
            .Where(sp => sp.ProductId == productId && sp.IsPreferred)
            .FirstOrDefaultAsync();

        if (preferredSupplier == null)
        {
            preferredSupplier = await supplierProductsQuery
                .Where(sp => sp.ProductId == productId)
                .FirstOrDefaultAsync();
        }

        if (preferredSupplier != null)
        {
            // Scale setup cost based on min order quantity or lead time
            setupCost = 50.0 + (preferredSupplier.LeadTimeDays * 2.0);
        }

        // 3. Holding Cost (H) - 15% of product's cost price
        double holdingCost = (double)product.CostPrice * 0.15;
        if (holdingCost == 0)
        {
            holdingCost = 1.5; // Default holding cost to avoid division by zero
        }

        // 4. EOQ Formula = sqrt(2DS / H)
        double eoq = Math.Sqrt((2.0 * annualDemand * setupCost) / holdingCost);

        return Math.Round(eoq, 2);
    }

    public async Task<decimal> GetInventoryValuationAsync(Guid productId, Guid warehouseId, ValuationMethod method)
    {
        var product = await _uow.Repository<Product>().GetByIdAsync(productId);
        if (product == null) return 0;

        var stockLevel = await _uow.Repository<StockLevel>()
            .Query()
            .Where(sl => sl.ProductId == productId && sl.WarehouseId == warehouseId)
            .FirstOrDefaultAsync();

        if (stockLevel == null || stockLevel.QuantityOnHand <= 0)
        {
            return 0;
        }

        int qtyOnHand = stockLevel.QuantityOnHand;

        // Fetch all Goods Receipt Items for the product and warehouse
        var grItems = await _uow.Repository<GoodsReceiptItem>()
            .Query()
            .Include(gri => gri.GoodsReceipt)
            .Include(gri => gri.PurchaseOrderItem)
            .Where(gri => gri.PurchaseOrderItem.ProductId == productId &&
                        gri.GoodsReceipt.WarehouseId == warehouseId &&
                        (gri.GoodsReceipt.Status == GoodsReceiptStatus.Accepted || 
                         gri.GoodsReceipt.Status == GoodsReceiptStatus.PartiallyAccepted))
            .ToListAsync();

        if (method == ValuationMethod.WeightedAverage)
        {
            int totalReceived = grItems.Sum(gri => gri.QuantityReceived - gri.QuantityRejected);
            if (totalReceived <= 0)
            {
                // Fallback to current cost price
                return qtyOnHand * product.CostPrice;
            }

            decimal totalCost = grItems.Sum(gri =>
                (gri.QuantityReceived - gri.QuantityRejected) * gri.PurchaseOrderItem.UnitPrice);

            decimal averageCost = totalCost / totalReceived;
            return qtyOnHand * averageCost;
        }
        else // FIFO
        {
            // ending inventory consists of the *most recently received* batches
            var sortedReceipts = grItems
                .OrderByDescending(gri => gri.GoodsReceipt.ReceivedDate)
                .ToList();

            decimal valuation = 0;
            int remainingToValue = qtyOnHand;

            foreach (var item in sortedReceipts)
            {
                int availableInBatch = item.QuantityReceived - item.QuantityRejected;
                if (availableInBatch <= 0) continue;

                if (remainingToValue <= availableInBatch)
                {
                    valuation += remainingToValue * item.PurchaseOrderItem.UnitPrice;
                    remainingToValue = 0;
                    break;
                }
                else
                {
                    valuation += availableInBatch * item.PurchaseOrderItem.UnitPrice;
                    remainingToValue -= availableInBatch;
                }
            }

            // If we still have quantities not covered by GRNs, value them using the base cost price
            if (remainingToValue > 0)
            {
                valuation += remainingToValue * product.CostPrice;
            }

            return valuation;
        }
    }
}
