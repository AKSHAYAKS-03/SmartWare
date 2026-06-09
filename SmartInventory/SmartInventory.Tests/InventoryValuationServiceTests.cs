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

public class InventoryValuationServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _uow;
    private readonly InventoryValuationService _service;

    public InventoryValuationServiceTests()
    {
        _context = TestDbContextFactory.Create();

        var products = new Mock<IProductRepository>();
        var suppliers = new Mock<ISupplierRepository>();
        var purchaseOrders = new Mock<IPurchaseOrderRepository>();
        var transfers = new Mock<ITransferRepository>();
        var barcodes = new Mock<IBarcodeRepository>();
        var notifications = new Mock<INotificationRepository>();
        var stockLevels = new Mock<IStockLevelRepository>();

        _uow = new UnitOfWork(_context, products.Object, suppliers.Object,
            purchaseOrders.Object, transfers.Object, barcodes.Object,
            notifications.Object, stockLevels.Object);

        _service = new InventoryValuationService(_uow);
    }

    // Prevents incorrect weighted-average cost after a new receipt
    [Fact]
    public async Task RecalculateWacAsync_WhenReceiptIncreasesExistingStock_UpdatesProductCostPrice()
    {
        // Arrange
        var category = new Category { Id = Guid.NewGuid(), Name = "Valuation", Description = "Valuation category" };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Valued Widget",
            SKU = "VAL-001",
            CostPrice = 10m,
            SellingPrice = 15m,
            ReorderPoint = 5,
            IsActive = true,
            CategoryId = category.Id,
            Category = category
        };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), Name = "Valuation WH", Code = "WH-VAL" };
        var stock = new StockLevel
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            QuantityOnHand = 10,
            QuantityReserved = 0,
            QuantityOnOrder = 0,
            LastUpdated = DateTime.UtcNow
        };

        await _context.Categories.AddAsync(category);
        await _context.Products.AddAsync(product);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.StockLevels.AddAsync(stock);
        await _context.SaveChangesAsync();

        // Act
        await _service.RecalculateWacAsync(product.Id, 5, 20m);

        // Assert
        var updatedProduct = await _context.Products.SingleAsync(p => p.Id == product.Id);
        Assert.Equal(13.33m, updatedProduct.CostPrice);
        Assert.NotNull(updatedProduct.UpdatedAt);
    }

    // Prevents unnecessary writes when no quantity was actually received
    [Fact]
    public async Task RecalculateWacAsync_WhenNewQuantityIsZero_DoesNotChangeCostPrice()
    {
        // Arrange
        var category = new Category { Id = Guid.NewGuid(), Name = "Valuation", Description = "Valuation category" };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "NoOp Widget",
            SKU = "VAL-002",
            CostPrice = 11m,
            SellingPrice = 15m,
            ReorderPoint = 5,
            IsActive = true,
            CategoryId = category.Id,
            Category = category
        };

        await _context.Categories.AddAsync(category);
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        // Act
        await _service.RecalculateWacAsync(product.Id, 0, 50m);

        // Assert
        var unchangedProduct = await _context.Products.SingleAsync(p => p.Id == product.Id);
        Assert.Equal(11m, unchangedProduct.CostPrice);
        Assert.Null(unchangedProduct.UpdatedAt);
    }

    // Prevents valuation updates for missing products from corrupting other records
    [Fact]
    public async Task RecalculateWacAsync_WhenProductDoesNotExist_DoesNothing()
    {
        // Arrange
        var category = new Category { Id = Guid.NewGuid(), Name = "Valuation", Description = "Valuation category" };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), Name = "Valuation WH", Code = "WH-VAL" };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Existing Widget",
            SKU = "VAL-003",
            CostPrice = 14m,
            SellingPrice = 20m,
            ReorderPoint = 5,
            IsActive = true,
            CategoryId = category.Id,
            Category = category
        };

        await _context.Categories.AddAsync(category);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        var missingProductId = Guid.NewGuid();

        // Act
        await _service.RecalculateWacAsync(missingProductId, 5, 100m);

        // Assert
        var untouchedProduct = await _context.Products.SingleAsync(p => p.Id == product.Id);
        Assert.Equal(14m, untouchedProduct.CostPrice);
        Assert.Null(untouchedProduct.UpdatedAt);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
