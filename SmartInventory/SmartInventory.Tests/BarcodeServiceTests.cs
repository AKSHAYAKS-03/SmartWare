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

public class BarcodeServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly BarcodeService _service;

    public BarcodeServiceTests()
    {
        _context = TestDbContextFactory.Create();

        var products = new Mock<IProductRepository>();
        var suppliers = new Mock<ISupplierRepository>();
        var purchaseOrders = new Mock<IPurchaseOrderRepository>();
        var transfers = new Mock<ITransferRepository>();
        var barcodes = new Mock<IBarcodeRepository>();
        var notifications = new Mock<INotificationRepository>();
        var stockLevels = new Mock<IStockLevelRepository>();

        var uow = new UnitOfWork(
            _context,
            products.Object,
            suppliers.Object,
            purchaseOrders.Object,
            transfers.Object,
            barcodes.Object,
            notifications.Object,
            stockLevels.Object);

        _service = new BarcodeService(uow);
    }

    [Fact]
    public async Task GenerateBarcodeRecordAsync_WhenNoneExists_CreatesPrimaryBarcode()
    {
        var product = await SeedProductAsync();

        var result = await _service.GenerateBarcodeRecordAsync(new BarcodeGenerateRequestDto
        {
            ProductId = product.Id,
            Type = BarcodeType.Code128
        });

        var barcode = await _context.Barcodes.SingleAsync(b => b.ProductId == product.Id);

        Assert.Equal(product.Id, result.ProductId);
        Assert.Equal(product.SKU, result.BarcodeValue);
        Assert.True(result.IsPrimary);
        Assert.Equal(BarcodeType.Code128, result.BarcodeType);
        Assert.Equal(1, await _context.Barcodes.CountAsync());
        Assert.Equal(product.SKU, barcode.BarcodeValue);
    }

    [Fact]
    public async Task GenerateBarcodeRecordAsync_WhenBarcodeExists_ThrowsConflict()
    {
        var product = await SeedProductAsync();
        await SeedBarcodeAsync(product.Id, product.SKU, BarcodeType.Code128);

        var ex = await Assert.ThrowsAsync<BarcodeAlreadyExistsException>(() =>
            _service.GenerateBarcodeRecordAsync(new BarcodeGenerateRequestDto
            {
                ProductId = product.Id,
                Type = BarcodeType.QRCode
            }));

        Assert.Equal(409, ex.StatusCode);
        Assert.Equal("BARCODE_ALREADY_EXISTS", ex.ErrorCode);
        Assert.Equal("Product already has a barcode. Use the update barcode endpoint if changes are required.", ex.Message);
        Assert.Equal(1, await _context.Barcodes.CountAsync());
    }

    [Fact]
    public async Task UpdateBarcodeRecordAsync_UpdatesExistingBarcode()
    {
        var product = await SeedProductAsync();
        var barcode = await SeedBarcodeAsync(product.Id, "OLD-VALUE", BarcodeType.Code128);

        var result = await _service.UpdateBarcodeRecordAsync(product.Id, new BarcodeUpdateDto
        {
            Type = BarcodeType.QRCode
        });

        var updated = await _context.Barcodes.SingleAsync(b => b.Id == barcode.Id);

        Assert.Equal(product.Id, result.ProductId);
        Assert.Equal(product.SKU, result.BarcodeValue);
        Assert.Equal(BarcodeType.QRCode, result.BarcodeType);
        Assert.True(result.IsPrimary);
        Assert.Equal(BarcodeType.QRCode, updated.BarcodeType);
        Assert.Equal(product.SKU, updated.BarcodeValue);
        Assert.True(updated.IsPrimary);
    }

    private async Task<Product> SeedProductAsync()
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Test Category",
            Description = "Test"
        };

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            SKU = "PRD-2026-000001",
            Description = "Test",
            UnitOfMeasure = UnitOfMeasure.Piece,
            CostPrice = 10m,
            SellingPrice = 12m,
            ReorderPoint = 1,
            ReorderQuantity = 2,
            CategoryId = category.Id,
            Category = category,
            IsActive = true
        };

        await _context.Categories.AddAsync(category);
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        return product;
    }

    private async Task<Barcode> SeedBarcodeAsync(Guid productId, string barcodeValue, BarcodeType type)
    {
        var barcode = new Barcode
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            BarcodeValue = barcodeValue,
            BarcodeType = type,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Barcodes.AddAsync(barcode);
        await _context.SaveChangesAsync();
        return barcode;
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
