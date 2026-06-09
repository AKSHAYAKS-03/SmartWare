using Microsoft.EntityFrameworkCore;
using Moq;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;
using SmartInventory.Repository;
using SmartInventory.Repository.Repositories;
using SmartInventory.Service.Services;
using Xunit;

namespace SmartInventory.Tests;

public class ProductServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _uow;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IBarcodeService> _barcodeMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<ISequenceNumberGenerator> _sequenceMock;
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        _context = TestDbContextFactory.Create();

        _uow = new UnitOfWork(
            _context,
            Mock.Of<IProductRepository>(),
            Mock.Of<ISupplierRepository>(),
            Mock.Of<IPurchaseOrderRepository>(),
            Mock.Of<ITransferRepository>(),
            Mock.Of<IBarcodeRepository>(),
            Mock.Of<INotificationRepository>(),
            Mock.Of<IStockLevelRepository>());

        _currentUserMock = new Mock<ICurrentUserService>();
        _barcodeMock = new Mock<IBarcodeService>();
        _cacheMock = new Mock<ICacheService>();
        _sequenceMock = new Mock<ISequenceNumberGenerator>();

        _cacheMock.Setup(x => x.GetAsync<ProductResponseDto>(It.IsAny<string>()))
            .ReturnsAsync((ProductResponseDto?)null);
        _cacheMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);
        _cacheMock.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _sequenceMock.Setup(x => x.GenerateAsync("seq_products", "PRD"))
            .ReturnsAsync("PRD-0001");

        _service = new ProductService(
            _uow,
            _currentUserMock.Object,
            _barcodeMock.Object,
            _cacheMock.Object,
            _sequenceMock.Object);
    }

    // Prevents product creation from skipping the primary barcode used in downstream scans
    [Fact]
    public async Task CreateProductAsync_ValidProduct_CreatesProductAndPrimaryBarcode()
    {
        // Arrange
        var category = await SeedCategoryAsync();
        var dto = new ProductCreateDto
        {
            Name = "Widget",
            Description = "Inventory widget",
            UnitOfMeasure = UnitOfMeasure.Piece,
            CostPrice = 10m,
            SellingPrice = 15m,
            ReorderPoint = 5,
            ReorderQuantity = 10,
            CategoryId = category.Id,
            IsActive = true
        };

        // Act
        var result = await _service.CreateProductAsync(dto);

        // Assert
        Assert.Equal("PRD-0001", result.SKU);
        Assert.Equal("Widget", result.Name);
        Assert.Equal(category.Id, result.CategoryId);
        Assert.Equal(category.Name, result.CategoryName);

        var savedProduct = await _context.Products.FirstAsync(p => p.Id == result.Id);
        Assert.Equal("PRD-0001", savedProduct.SKU);
        Assert.True(await _context.Barcodes.AnyAsync(b => b.ProductId == result.Id && b.IsPrimary && b.BarcodeValue == "PRD-0001"));
    }

    // Prevents safe catalog edits from being blocked when no stock is tied to the product dimensions
    [Fact]
    public async Task UpdateProductAsync_NoDimensionChange_UpdatesProductAndClearsCache()
    {
        // Arrange
        var category = await SeedCategoryAsync();
        var product = await SeedProductAsync(category, includeStock: false);

        var dto = new ProductUpdateDto
        {
            Name = "Widget Updated",
            Description = "Updated description",
            UnitOfMeasure = UnitOfMeasure.Box,
            CostPrice = 12m,
            SellingPrice = 18m,
            ReorderPoint = 7,
            ReorderQuantity = 11,
            CategoryId = category.Id,
            IsActive = true,
            ImagePath = "/images/widget.png",
            Length = product.Length,
            Width = product.Width,
            Height = product.Height,
            WeightKg = product.WeightKg
        };

        // Act
        var result = await _service.UpdateProductAsync(product.Id, dto);

        // Assert
        Assert.Equal("Widget Updated", result.Name);
        Assert.Equal(UnitOfMeasure.Box, result.UnitOfMeasure);
        Assert.True(result.IsActive);
        _cacheMock.Verify(x => x.RemoveAsync($"product:id:{product.Id}"), Times.Once);
    }

    // Prevents dimensional changes from corrupting capacity calculations while stock still exists
    [Fact]
    public async Task UpdateProductAsync_DimensionChangeWithActiveStock_ThrowsBusinessRuleException()
    {
        // Arrange
        var category = await SeedCategoryAsync();
        var product = await SeedProductAsync(category, includeStock: true, stockQty: 3);

        var dto = new ProductUpdateDto
        {
            Name = product.Name,
            Description = product.Description,
            UnitOfMeasure = product.UnitOfMeasure,
            CostPrice = product.CostPrice,
            SellingPrice = product.SellingPrice,
            ReorderPoint = product.ReorderPoint,
            ReorderQuantity = product.ReorderQuantity,
            CategoryId = category.Id,
            IsActive = product.IsActive,
            ImagePath = product.ImagePath,
            Length = product.Length + 1,
            Width = product.Width,
            Height = product.Height,
            WeightKg = product.WeightKg
        };

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.UpdateProductAsync(product.Id, dto));
    }

    // Prevents stock-bearing products from being hard removed while still allowing zero-stock cleanup
    [Fact]
    public async Task DeleteProductAsync_NoStock_SoftDeletesAndWithStock_ThrowsBusinessRuleException()
    {
        // Arrange
        var category = await SeedCategoryAsync();
        var deletableProduct = await SeedProductAsync(category, includeStock: false);
        var protectedProduct = await SeedProductAsync(category, includeStock: true, stockQty: 2);

        // Act
        await _service.DeleteProductAsync(deletableProduct.Id);

        // Assert
        var deletedRow = await _context.Products.IgnoreQueryFilters().FirstAsync(p => p.Id == deletableProduct.Id);
        Assert.False(deletedRow.IsActive);
        _cacheMock.Verify(x => x.RemoveAsync($"product:id:{deletableProduct.Id}"), Times.Once);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.DeleteProductAsync(protectedProduct.Id));
    }

    // Prevents ABC categories from staying stale after warehouse-level stock value shifts
    [Fact]
    public async Task UpdateAbcCategoriesAsync_PersistsClassificationsPerWarehouse()
    {
        // Arrange
        var category = await SeedCategoryAsync();
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Name = "Main Warehouse",
            Code = "WH-ABC",
            State = "KA",
            IsActive = true
        };

        var productA = await SeedProductAsync(category, sku: "SKU-A", name: "Product A", sellingPrice: 10m, costPrice: 5m, includeStock: false);
        var productB = await SeedProductAsync(category, sku: "SKU-B", name: "Product B", sellingPrice: 10m, costPrice: 5m, includeStock: false);
        var productC = await SeedProductAsync(category, sku: "SKU-C", name: "Product C", sellingPrice: 10m, costPrice: 5m, includeStock: false);

        await _context.Warehouses.AddAsync(warehouse);
        await _context.StockLevels.AddRangeAsync(
            new StockLevel { Id = Guid.NewGuid(), ProductId = productA.Id, WarehouseId = warehouse.Id, QuantityOnHand = 7 },
            new StockLevel { Id = Guid.NewGuid(), ProductId = productB.Id, WarehouseId = warehouse.Id, QuantityOnHand = 2 },
            new StockLevel { Id = Guid.NewGuid(), ProductId = productC.Id, WarehouseId = warehouse.Id, QuantityOnHand = 1 });
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateAbcCategoriesAsync(warehouse.Id);

        // Assert
        var savedA = await _context.Products.IgnoreQueryFilters().FirstAsync(p => p.Id == productA.Id);
        var savedB = await _context.Products.IgnoreQueryFilters().FirstAsync(p => p.Id == productB.Id);
        var savedC = await _context.Products.IgnoreQueryFilters().FirstAsync(p => p.Id == productC.Id);

        Assert.Equal("A", savedA.AbcCategory);
        Assert.Equal("B", savedB.AbcCategory);
        Assert.Equal("C", savedC.AbcCategory);
    }

    private async Task<Category> SeedCategoryAsync()
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Hardware",
            Slug = $"hardware-{Guid.NewGuid():N}",
            Description = "Hardware items",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Categories.AddAsync(category);
        await _context.SaveChangesAsync();

        return category;
    }

    private async Task<Product> SeedProductAsync(
        Category category,
        string? sku = null,
        string? name = null,
        decimal? sellingPrice = null,
        decimal? costPrice = null,
        bool includeStock = false,
        int stockQty = 0)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name ?? "Widget",
            SKU = sku ?? $"SKU-{Guid.NewGuid():N}",
            Description = "Product description",
            UnitOfMeasure = UnitOfMeasure.Piece,
            CostPrice = costPrice ?? 10m,
            SellingPrice = sellingPrice ?? 15m,
            ReorderPoint = 5,
            ReorderQuantity = 10,
            CategoryId = category.Id,
            Category = category,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Length = 10m,
            Width = 5m,
            Height = 2m,
            WeightKg = 1m
        };

        await _context.Products.AddAsync(product);

        if (includeStock)
        {
            var warehouse = await SeedWarehouseAsync();
            await _context.StockLevels.AddAsync(new StockLevel
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                QuantityOnHand = stockQty
            });
        }

        await _context.SaveChangesAsync();
        return product;
    }

    private async Task<Warehouse> SeedWarehouseAsync()
    {
        var existing = await _context.Warehouses.FirstOrDefaultAsync(w => w.Code == "WH-PROD");
        if (existing != null)
            return existing;

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Name = "Product Warehouse",
            Code = "WH-PROD",
            State = "KA",
            IsActive = true
        };

        await _context.Warehouses.AddAsync(warehouse);
        await _context.SaveChangesAsync();
        return warehouse;
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
