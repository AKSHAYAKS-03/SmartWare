using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
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
using MediatR;

namespace SmartInventory.Tests;

/// <summary>
/// Unit tests for StockAdjustmentService.
///   — CreateAdjustmentAsync: auto-approve low-variance, pending high-variance
///   — ApproveAdjustmentAsync: approve/reject transitions, stock level updates
///   — GetAdjustmentByIdAsync: not-found throw
/// </summary>
public class StockAdjustmentServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _uow;
    private readonly StockAdjustmentService _service;
    private readonly Mock<INotificationService> _notificationMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;

    public StockAdjustmentServiceTests()
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

        _notificationMock = new Mock<INotificationService>();
        _notificationMock.Setup(n => n.SendNotificationAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationChannel>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);
        _notificationMock.Setup(n => n.SendLowStockAlertAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _currentUserServiceMock.Setup(c => c.UserId).Returns(Guid.NewGuid()); // Default

        var logger = Mock.Of<ILogger<StockAdjustmentService>>();
        var memoryCacheMock = new Mock<ICacheService>();
        var publisherMock = new Mock<IPublisher>();
        var authorizationServiceMock = new Mock<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
        var varianceResolverMock = new Mock<ITransferVarianceResolver>();
        varianceResolverMock.Setup(v => v.TryResolveTransferVarianceAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        _service = new StockAdjustmentService(_uow, _notificationMock.Object, logger, _currentUserServiceMock.Object, memoryCacheMock.Object, publisherMock.Object, authorizationServiceMock.Object, varianceResolverMock.Object);
    }

    private async Task<(Product product, Warehouse warehouse, User user)> SeedAsync(
        int costPrice = 10, int currentStock = 100)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Hardware", Description = "Tools" };
        var product = new Product
        {
            Id = Guid.NewGuid(), Name = "Widget", SKU = "WGT-001",
            CostPrice = costPrice, SellingPrice = 15m, ReorderPoint = 10, IsActive = true, CategoryId = category.Id, Category = category
        };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), Name = "WH1", Code = "WH1" };
        var role = await _context.Roles.FirstAsync(r => r.Name == "Staff");
        var user = new User
        {
            Id = Guid.NewGuid(), FullName = "Staff User", Email = "staff@test.com",
            PasswordHash = "hash", IsActive = true, Status = UserStatus.Active, RoleId = role.Id, Role = role
        };
        var sl = new StockLevel
        {
            Id = Guid.NewGuid(), ProductId = product.Id, WarehouseId = warehouse.Id,
            QuantityOnHand = currentStock
        };

        await _context.Categories.AddAsync(category);
        await _context.Products.AddAsync(product);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.StockLevels.AddAsync(sl);
        await _context.Users.AddAsync(user);

        await _context.SaveChangesAsync();

        return (product, warehouse, user);
    }

    [Fact]
    public async Task CreateAdjustmentAsync_SmallVariance_AutoApproves()
    {
        // Arrange: current stock = 100, adjust to 101 = 1% variance → auto-approve
        var (product, warehouse, user) = await SeedAsync(costPrice: 10, currentStock: 100);
        _currentUserServiceMock.Setup(c => c.UserId).Returns(user.Id);

        var dto = new StockAdjustmentCreateDto
        {
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            PerformedBy = user.Id,
            QuantityAfter = 101, // 1% change, $10 value — below both thresholds
            Reason = AdjustmentReason.CycleCount,
            Notes = "Small adjustment"
        };

        // Act
        var result = await _service.CreateAdjustmentAsync(dto);

        // Assert
        Assert.Equal(AdjustmentStatus.Approved, result.Status);
        Assert.Equal(101, result.QuantityAfter);

        // Verify stock level was updated
        var sl = await _context.StockLevels
            .FirstAsync(s => s.ProductId == product.Id && s.WarehouseId == warehouse.Id);
        Assert.Equal(101, sl.QuantityOnHand);

        // Verify a stock movement was logged
        var movement = await _context.StockMovements.FirstOrDefaultAsync(m => m.ProductId == product.Id);
        Assert.NotNull(movement);
        Assert.Equal(MovementType.Adjustment, movement.MovementType);
    }

    [Fact]
    public async Task CreateAdjustmentAsync_LargeVariance_ThrowsApprovalRequired()
    {
        // Arrange: 100 → 50 = 50% change, cost $10 each → $500 variance — both thresholds breached
        var (product, warehouse, user) = await SeedAsync(costPrice: 10, currentStock: 100);
        _currentUserServiceMock.Setup(c => c.UserId).Returns(user.Id);

        var dto = new StockAdjustmentCreateDto
        {
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            PerformedBy = user.Id,
            QuantityAfter = 50,
            Reason = AdjustmentReason.Theft,
            Notes = "Big loss"
        };

        // Act & Assert: service throws ApprovalRequiredException but still persists the Pending record
        await Assert.ThrowsAsync<ApprovalRequiredException>(() =>
            _service.CreateAdjustmentAsync(dto));

        // The record should be saved with Pending status
        var saved = await _context.StockAdjustments.FirstOrDefaultAsync(a => a.ProductId == product.Id);
        Assert.NotNull(saved);
        Assert.Equal(AdjustmentStatus.Pending, saved.Status);

        // Stock level must NOT have changed (not yet approved)
        var sl = await _context.StockLevels
            .FirstAsync(s => s.ProductId == product.Id && s.WarehouseId == warehouse.Id);
        Assert.Equal(100, sl.QuantityOnHand); // unchanged
    }

    [Fact]
    public async Task ApproveAdjustmentAsync_Approve_UpdatesStock()
    {
        // Arrange: seed a Pending adjustment directly
        var (product, warehouse, user) = await SeedAsync(costPrice: 10, currentStock: 100);
        var role = await _context.Roles.FirstAsync(r => r.Name == "Manager");
        var manager = new User
        {
            Id = Guid.NewGuid(), FullName = "Manager", Email = "mgr@test.com",
            PasswordHash = "hash", IsActive = true, Status = UserStatus.Active, RoleId = role.Id, Role = role
        };
        await _context.Users.AddAsync(manager);

        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ-20250101-ABCDEF",
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Reason = AdjustmentReason.CycleCount,
            QuantityBefore = 100,
            QuantityAfter = 50,
            QuantityChange = -50,
            Status = AdjustmentStatus.Pending,
            PerformedBy = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        await _context.StockAdjustments.AddAsync(adjustment);
        await _context.SaveChangesAsync();

        var dto = new StockAdjustmentApprovalDto
        {
            Approve = true
        };
        
        _currentUserServiceMock.Setup(c => c.UserId).Returns(manager.Id);

        // Act
        var result = await _service.ApproveAdjustmentAsync(adjustment.Id, dto);

        // Assert
        Assert.Equal(AdjustmentStatus.Approved, result.Status);
        Assert.Equal(manager.Id, result.ApprovedBy);

        // Verify stock level was updated to QuantityAfter
        var sl = await _context.StockLevels
            .FirstAsync(s => s.ProductId == product.Id && s.WarehouseId == warehouse.Id);
        Assert.Equal(50, sl.QuantityOnHand);
    }

    [Fact]
    public async Task ApproveAdjustmentAsync_Reject_DoesNotUpdateStock()
    {
        // Arrange
        var (product, warehouse, user) = await SeedAsync(costPrice: 10, currentStock: 100);
        var role = await _context.Roles.FirstAsync(r => r.Name == "Manager");
        var manager = new User
        {
            Id = Guid.NewGuid(), FullName = "Manager", Email = "mgr@test.com",
            PasswordHash = "hash", IsActive = true, Status = UserStatus.Active, RoleId = role.Id, Role = role
        };
        await _context.Users.AddAsync(manager);

        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(), AdjustmentNumber = "ADJ-20250101-XYZABC",
            ProductId = product.Id, WarehouseId = warehouse.Id,
            Reason = AdjustmentReason.Theft,
            QuantityBefore = 100, QuantityAfter = 50, QuantityChange = -50,
            Status = AdjustmentStatus.Pending, PerformedBy = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        await _context.StockAdjustments.AddAsync(adjustment);
        await _context.SaveChangesAsync();

        // Act
        _currentUserServiceMock.Setup(c => c.UserId).Returns(manager.Id);
        var result = await _service.ApproveAdjustmentAsync(adjustment.Id,
            new StockAdjustmentApprovalDto { Approve = false });

        // Assert: rejected, stock unchanged
        Assert.Equal(AdjustmentStatus.Rejected, result.Status);

        var sl = await _context.StockLevels
            .FirstAsync(s => s.ProductId == product.Id && s.WarehouseId == warehouse.Id);
        Assert.Equal(100, sl.QuantityOnHand); // original unchanged
    }

    [Fact]
    public async Task GetAdjustmentByIdAsync_NotFound_ThrowsNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetAdjustmentByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ApproveAdjustmentAsync_AlreadyApproved_ThrowsBusinessRuleException()
    {
        // Arrange: seed an already-approved adjustment
        var (product, warehouse, user) = await SeedAsync();
        var role = await _context.Roles.FirstAsync(r => r.Name == "Manager");
        var manager = new User
        {
            Id = Guid.NewGuid(), FullName = "Manager", Email = "mgr2@test.com",
            PasswordHash = "hash", IsActive = true, Status = UserStatus.Active, RoleId = role.Id, Role = role
        };
        await _context.Users.AddAsync(manager);

        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(), AdjustmentNumber = "ADJ-APPROVED",
            ProductId = product.Id, WarehouseId = warehouse.Id,
            Reason = AdjustmentReason.CycleCount,
            QuantityBefore = 100, QuantityAfter = 105, QuantityChange = 5,
            Status = AdjustmentStatus.Approved, // Already approved!
            PerformedBy = user.Id, CreatedAt = DateTime.UtcNow
        };
        await _context.StockAdjustments.AddAsync(adjustment);
        await _context.SaveChangesAsync();

        // Act & Assert
        _currentUserServiceMock.Setup(c => c.UserId).Returns(manager.Id);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.ApproveAdjustmentAsync(adjustment.Id,
                new StockAdjustmentApprovalDto { Approve = true }));
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
