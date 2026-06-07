using Microsoft.EntityFrameworkCore;
using Moq;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;
using SmartInventory.Repository;
using SmartInventory.Repository.Repositories;
using SmartInventory.Service.Services;
using Xunit;

namespace SmartInventory.Tests;

/// <summary>
/// Integration-style tests for StockLevelService using EF Core InMemory + real UnitOfWork.
/// Uses Moq for all specialized repository interfaces required by UnitOfWork's constructor.
/// All tests are deterministic and run in isolation via unique database names.
/// </summary>
public class StockLevelServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UnitOfWork _uow;
    private readonly StockLevelService _service;

    public StockLevelServiceTests()
    {
        _context = TestDbContextFactory.Create();

        // Mock all specialized repositories required by UnitOfWork constructor
        var products = new Mock<IProductRepository>();
        var suppliers = new Mock<ISupplierRepository>();
        var purchaseOrders = new Mock<IPurchaseOrderRepository>();
        var transfers = new Mock<ITransferRepository>();
        var barcodes = new Mock<IBarcodeRepository>();
        var notifications = new Mock<INotificationRepository>();
        var stockLevels = new Mock<IStockLevelRepository>();

        _uow = new UnitOfWork(
            _context,
            products.Object,
            suppliers.Object,
            purchaseOrders.Object,
            transfers.Object,
            barcodes.Object,
            notifications.Object,
            stockLevels.Object);

        _service = new StockLevelService(_uow);
    }

    [Fact]
    public async Task GetAbcClassificationAsync_ClassifiesCorrectly()
    {
        // Arrange: Seed 3 products with different stock levels & selling prices to create economic value tiers
        var category = new Category { Id = Guid.NewGuid(), Name = "Hardware", Description = "Tools" };
        var productA = new Product { Id = Guid.NewGuid(), Name = "Product A", SKU = "PRD-A", SellingPrice = 100m, CostPrice = 50m, IsActive = true, CategoryId = category.Id, Category = category }; // Value: 7 * 100 = 700 (Class A)
        var productB = new Product { Id = Guid.NewGuid(), Name = "Product B", SKU = "PRD-B", SellingPrice = 50m, CostPrice = 25m, IsActive = true, CategoryId = category.Id, Category = category };  // Value: 4 * 50 = 200 (Class B)
        var productC = new Product { Id = Guid.NewGuid(), Name = "Product C", SKU = "PRD-C", SellingPrice = 10m, CostPrice = 5m, IsActive = true, CategoryId = category.Id, Category = category };   // Value: 10 * 10 = 100 (Class C)

        var warehouseId = Guid.NewGuid();
        var warehouse = new Warehouse { Id = warehouseId, Name = "Main Warehouse", Code = "WH-01" };

        var slA = new StockLevel { Id = Guid.NewGuid(), ProductId = productA.Id, WarehouseId = warehouseId, QuantityOnHand = 7 };
        var slB = new StockLevel { Id = Guid.NewGuid(), ProductId = productB.Id, WarehouseId = warehouseId, QuantityOnHand = 4 };
        var slC = new StockLevel { Id = Guid.NewGuid(), ProductId = productC.Id, WarehouseId = warehouseId, QuantityOnHand = 10 };

        await _context.Categories.AddAsync(category);
        await _context.Products.AddRangeAsync(productA, productB, productC);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.StockLevels.AddRangeAsync(slA, slB, slC);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAbcClassificationAsync(warehouseId);

        // Assert
        Assert.Equal(3, result.Count);

        var classA = result.First(r => r.ProductId == productA.Id);
        var classB = result.First(r => r.ProductId == productB.Id);
        var classC = result.First(r => r.ProductId == productC.Id);

        Assert.Equal("A", classA.Class);
        Assert.Equal("B", classB.Class);
        Assert.Equal("C", classC.Class);
    }

    [Fact]
    public async Task CalculateEoqAsync_CalculatesExpectedQuantity()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var category = new Category { Id = Guid.NewGuid(), Name = "Hardware", Description = "Tools" };
        var product = new Product { Id = productId, Name = "Product A", SKU = "PRD-A", CostPrice = 100m, SellingPrice = 150m, CategoryId = category.Id, Category = category };
        var warehouse = new Warehouse { Id = warehouseId, Name = "WH1", Code = "WH1" };

        var role = new Role { Id = Guid.NewGuid(), Name = "WarehouseStaff" };
        var user = new User { Id = Guid.NewGuid(), Email = "test@test.com", FullName = "Test User", PasswordHash = "hash", RoleId = role.Id };

        // Outbound movements in past 90 days to simulate demand
        var movements = new List<StockMovement>
        {
            new() { Id = Guid.NewGuid(), ProductId = productId, WarehouseId = warehouseId, MovementType = MovementType.Sale, Quantity = 10, CreatedAt = DateTime.UtcNow.AddDays(-10), PerformedBy = user.Id },
            new() { Id = Guid.NewGuid(), ProductId = productId, WarehouseId = warehouseId, MovementType = MovementType.Sale, Quantity = 15, CreatedAt = DateTime.UtcNow.AddDays(-30), PerformedBy = user.Id },
            new() { Id = Guid.NewGuid(), ProductId = productId, WarehouseId = warehouseId, MovementType = MovementType.TransferOut, Quantity = 5, CreatedAt = DateTime.UtcNow.AddDays(-60), PerformedBy = user.Id }
        };

        // Preferred supplier mapping
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Supplier", Email = "sup@test.com", Phone = "123", IsActive = true };
        var supplierProduct = new SupplierProduct
        {
            Id = Guid.NewGuid(), SupplierId = supplier.Id, ProductId = productId,
            IsPreferred = true,
            LeadTimeDays = 5 // Setup cost will be 50.0 + (5 * 2.0) = 60.0
        };

        await _context.Roles.AddAsync(role);
        await _context.Users.AddAsync(user);
        await _context.Suppliers.AddAsync(supplier);
        await _context.Categories.AddAsync(category);
        await _context.Products.AddAsync(product);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.StockMovements.AddRangeAsync(movements);
        await _context.SupplierProducts.AddAsync(supplierProduct);
        await _context.SaveChangesAsync();

        // 90 day demand = 30 units. Extrapolated Annual Demand (D) = 30 * 4 = 120 units.
        // Setup Cost (S) = 60.0
        // Holding Cost (H) = 100 * 0.15 = 15.0
        // EOQ = sqrt(2 * 120 * 60 / 15) = sqrt(14400 / 15) = sqrt(960) ≈ 30.98

        // Act
        var eoq = await _service.CalculateEoqAsync(productId, warehouseId);

        // Assert
        Assert.True(eoq > 30.0);
        Assert.True(eoq < 32.0);
    }

    [Fact]
    public async Task GetInventoryValuationAsync_FIFO_ValuatesCorrectly()
    {
        // Arrange: ending inventory = 15 units.
        // We received 10 units @ $8 each (most recent) and 10 units @ $5 each (older).
        // FIFO valuation should cover 10 units @ $8 and 5 units @ $5 = $80 + $25 = $105.
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var category = new Category { Id = Guid.NewGuid(), Name = "Hardware", Description = "Tools" };
        var product = new Product { Id = productId, Name = "Product A", SKU = "PRD-A", CostPrice = 6m, SellingPrice = 10m, CategoryId = category.Id, Category = category };
        var warehouse = new Warehouse { Id = warehouseId, Name = "WH1", Code = "WH1" };
        var stockLevel = new StockLevel { Id = Guid.NewGuid(), ProductId = productId, WarehouseId = warehouseId, QuantityOnHand = 15 };

        var role = new Role { Id = Guid.NewGuid(), Name = "WarehouseStaff" };
        var user = new User { Id = Guid.NewGuid(), Email = "test2@test.com", FullName = "Test2 User", PasswordHash = "hash", RoleId = role.Id };
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Supplier", Email = "sup@test.com", Phone = "123", IsActive = true };
        var po = new PurchaseOrder { Id = Guid.NewGuid(), SupplierId = supplier.Id, WarehouseId = warehouseId, Status = PurchaseOrderStatus.Received, ExpectedDelivery = DateTime.UtcNow, CreatedBy = user.Id };

        var gr1 = new GoodsReceipt { Id = Guid.NewGuid(), GrnNumber = "GRN-1", WarehouseId = warehouseId, Status = GoodsReceiptStatus.Accepted, ReceivedDate = DateTime.UtcNow.AddDays(-10), ReceivedBy = user.Id, PurchaseOrderId = po.Id }; // older
        var gr2 = new GoodsReceipt { Id = Guid.NewGuid(), GrnNumber = "GRN-2", WarehouseId = warehouseId, Status = GoodsReceiptStatus.Accepted, ReceivedDate = DateTime.UtcNow.AddDays(-2), ReceivedBy = user.Id, PurchaseOrderId = po.Id };  // newer

        var poItem1 = new PurchaseOrderItem { Id = Guid.NewGuid(), PurchaseOrderId = po.Id, ProductId = productId, UnitPrice = 5m };
        var poItem2 = new PurchaseOrderItem { Id = Guid.NewGuid(), PurchaseOrderId = po.Id, ProductId = productId, UnitPrice = 8m };

        var gri1 = new GoodsReceiptItem { Id = Guid.NewGuid(), GoodsReceiptId = gr1.Id, PurchaseOrderItemId = poItem1.Id, QuantityReceived = 10, PurchaseOrderItem = poItem1, GoodsReceipt = gr1 };
        var gri2 = new GoodsReceiptItem { Id = Guid.NewGuid(), GoodsReceiptId = gr2.Id, PurchaseOrderItemId = poItem2.Id, QuantityReceived = 10, PurchaseOrderItem = poItem2, GoodsReceipt = gr2 };

        await _context.Roles.AddAsync(role);
        await _context.Users.AddAsync(user);
        await _context.Categories.AddAsync(category);
        await _context.Suppliers.AddAsync(supplier);
        await _context.PurchaseOrders.AddAsync(po);
        await _context.Products.AddAsync(product);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.StockLevels.AddAsync(stockLevel);
        await _context.PurchaseOrderItems.AddRangeAsync(poItem1, poItem2);
        await _context.GoodsReceipts.AddRangeAsync(gr1, gr2);
        await _context.GoodsReceiptItems.AddRangeAsync(gri1, gri2);
        await _context.SaveChangesAsync();

        // Act
        var fifoValue = await _service.GetInventoryValuationAsync(productId, warehouseId, ValuationMethod.FIFO);
        var averageValue = await _service.GetInventoryValuationAsync(productId, warehouseId, ValuationMethod.WeightedAverage);

        // Assert
        Assert.Equal(105m, fifoValue);
        // Average value: (10*5 + 10*8)/20 = 6.5 per unit * 15 units = 97.5
        Assert.Equal(97.5m, averageValue);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
