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

public class WarehouseServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _uow;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<INotificationService> _notificationMock;
    private readonly Mock<ISequenceNumberGenerator> _sequenceMock;
    private readonly WarehouseService _service;

    public WarehouseServiceTests()
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

        _cacheMock = new Mock<ICacheService>();
        _notificationMock = new Mock<INotificationService>();
        _sequenceMock = new Mock<ISequenceNumberGenerator>();

        _cacheMock.Setup(x => x.GetAsync<WarehouseResponseDto>(It.IsAny<string>()))
            .ReturnsAsync((WarehouseResponseDto?)null);
        _cacheMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);
        _cacheMock.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _sequenceMock.Setup(x => x.GenerateAsync("seq_warehouses", "WH")).ReturnsAsync("WH-0001");
        _sequenceMock.Setup(x => x.GenerateAsync("seq_zones", "ZN")).ReturnsAsync("ZN-0001");
        _sequenceMock.Setup(x => x.GenerateAsync("seq_bins", "BIN")).ReturnsAsync("BIN-0001");

        _service = new WarehouseService(_uow, _cacheMock.Object, _notificationMock.Object, _sequenceMock.Object);
    }

    // Prevents warehouse creation from skipping the warehouse code sequence and cached read-back path
    [Fact]
    public async Task CreateWarehouseAsync_ValidWarehouse_CreatesWarehouseWithGeneratedCode()
    {
        // Arrange
        var dto = new WarehouseCreateDto
        {
            Name = "Main Warehouse",
            State = "KA",
            IsActive = true,
            AreaSqFt = 1000m,
            MaxVolumeCm3 = 5000m,
            MaxWeightKg = 1000m
        };

        // Act
        var result = await _service.CreateWarehouseAsync(dto);

        // Assert
        Assert.Equal("WH-0001", result.Code);
        Assert.Equal("Main Warehouse", result.Name);
        Assert.Equal(1000m, result.AreaSqFt);
    }

    // Prevents zone capacity from exceeding warehouse limits during layout expansion
    [Fact]
    public async Task CreateZoneAsync_WithinWarehouseCapacity_CreatesZone()
    {
        // Arrange
        var warehouse = await SeedWarehouseAsync(area: 100m, volume: 1000m, weight: 500m);
        var dto = new ZoneCreateDto
        {
            WarehouseId = warehouse.Id,
            Name = "Storage",
            ZoneType = ZoneType.Storage,
            AreaSqFt = 20m,
            MaxVolumeCm3 = 200m,
            MaxWeightKg = 100m,
            IsActive = true
        };

        // Act
        var result = await _service.CreateZoneAsync(dto);

        // Assert
        Assert.Equal("ZN-0001", result.Code);
        Assert.Equal(warehouse.Id, result.WarehouseId);
        Assert.Equal("Storage", result.Name);
    }

    // Prevents over-allocation of warehouse capacity when creating new zones
    [Fact]
    public async Task CreateZoneAsync_ExceedsWarehouseCapacity_ThrowsBusinessRuleException()
    {
        // Arrange
        var warehouse = await SeedWarehouseAsync(area: 10m, volume: 100m, weight: 50m);
        var dto = new ZoneCreateDto
        {
            WarehouseId = warehouse.Id,
            Name = "Too Large",
            ZoneType = ZoneType.Storage,
            AreaSqFt = 20m,
            MaxVolumeCm3 = 20m,
            MaxWeightKg = 20m,
            IsActive = true
        };

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateZoneAsync(dto));
    }

    // Prevents warehouse capacity reductions from invalidating already-created zones
    [Fact]
    public async Task UpdateWarehouseAsync_ReduceCapacityBelowExistingZones_ThrowsBusinessRuleException()
    {
        // Arrange
        var warehouse = await SeedWarehouseAsync(area: 100m, volume: 1000m, weight: 500m);
        await _context.WarehouseZones.AddAsync(new WarehouseZone
        {
            Id = Guid.NewGuid(),
            WarehouseId = warehouse.Id,
            Warehouse = warehouse,
            Name = "Existing Zone",
            Code = "ZN-EXIST",
            ZoneType = ZoneType.Storage,
            AreaSqFt = 40m,
            MaxVolumeCm3 = 400m,
            MaxWeightKg = 200m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var dto = new WarehouseUpdateDto
        {
            Name = warehouse.Name,
            Address = warehouse.Address,
            City = warehouse.City,
            State = warehouse.State,
            PostalCode = warehouse.PostalCode,
            Country = warehouse.Country,
            ContactPerson = warehouse.ContactPerson,
            ContactNumber = warehouse.ContactNumber,
            Email = warehouse.Email,
            GSTIN = warehouse.GSTIN,
            RegistrationNumber = warehouse.RegistrationNumber,
            ManagerId = warehouse.ManagerId,
            IsActive = warehouse.IsActive,
            AreaSqFt = 10m,
            MaxVolumeCm3 = 100m,
            MaxWeightKg = 50m
        };

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.UpdateWarehouseAsync(warehouse.Id, dto));
    }

    // Prevents deletion of warehouses that still contain stock while allowing clean decommissioning
    [Fact]
    public async Task DeleteWarehouseAsync_NoStock_SoftDeletesAndWithStock_ThrowsBusinessRuleException()
    {
        // Arrange
        var emptyWarehouse = await SeedWarehouseAsync(area: 100m, volume: 1000m, weight: 500m, code: "WH-EMPTY");
        var stockWarehouse = await SeedWarehouseAsync(area: 100m, volume: 1000m, weight: 500m, code: "WH-STOCK");
        var product = await SeedProductAsync();

        await _context.StockLevels.AddAsync(new StockLevel
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = stockWarehouse.Id,
            QuantityOnHand = 2
        });
        await _context.SaveChangesAsync();

        // Act
        await _service.DeleteWarehouseAsync(emptyWarehouse.Id);

        // Assert
        var deletedWarehouse = await _context.Warehouses.IgnoreQueryFilters().FirstAsync(w => w.Id == emptyWarehouse.Id);
        Assert.False(deletedWarehouse.IsActive);
        _cacheMock.Verify(x => x.RemoveAsync($"warehouse:id:{emptyWarehouse.Id}"), Times.Once);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.DeleteWarehouseAsync(stockWarehouse.Id));
    }

    private async Task<Warehouse> SeedWarehouseAsync(decimal area, decimal volume, decimal weight, string? code = null)
    {
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Name = code == null ? "Warehouse" : $"Warehouse {code}",
            Code = code ?? $"WH-{Guid.NewGuid():N}".Substring(0, 8),
            State = "KA",
            IsActive = true,
            AreaSqFt = area,
            MaxVolumeCm3 = volume,
            MaxWeightKg = weight,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Warehouses.AddAsync(warehouse);
        await _context.SaveChangesAsync();
        return warehouse;
    }

    private async Task<Product> SeedProductAsync()
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "General",
            Slug = $"general-{Guid.NewGuid():N}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Widget",
            SKU = $"WGT-{Guid.NewGuid():N}".Substring(0, 12),
            UnitOfMeasure = UnitOfMeasure.Piece,
            CostPrice = 10m,
            SellingPrice = 15m,
            ReorderPoint = 5,
            ReorderQuantity = 10,
            CategoryId = category.Id,
            Category = category,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Categories.AddAsync(category);
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();
        return product;
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
