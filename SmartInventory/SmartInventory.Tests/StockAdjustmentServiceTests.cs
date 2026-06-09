using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Security.Claims;
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
///   — GetAdjustmentByIdAsync: not-found throw and stale-stock warning details
/// </summary>
public class StockAdjustmentServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _uow;
    private readonly StockAdjustmentService _service;
    private readonly Mock<INotificationService> _notificationMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<IPublisher> _publisherMock;
    private readonly Mock<IAuthorizationService> _authorizationServiceMock;
    private readonly Mock<ITransferVarianceResolver> _varianceResolverMock;

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
        _notificationMock.Setup(n => n.SendOutOfStockAlertAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        _notificationMock.Setup(n => n.SendPendingStockAdjustmentApprovalAlertAsync(
            It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _currentUserServiceMock.Setup(c => c.UserId).Returns(Guid.NewGuid()); // Default
        _currentUserServiceMock.Setup(c => c.Principal).Returns((ClaimsPrincipal?)null);

        _cacheMock = new Mock<ICacheService>();
        _cacheMock.Setup(c => c.GetAsync<StockAdjustmentResponseDto>(It.IsAny<string>()))
            .ReturnsAsync((StockAdjustmentResponseDto?)null);
        _cacheMock.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<StockAdjustmentResponseDto>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);

        _publisherMock = new Mock<IPublisher>(MockBehavior.Loose);
        _authorizationServiceMock = new Mock<IAuthorizationService>(MockBehavior.Loose);
        _authorizationServiceMock
            .Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Failed());
        _varianceResolverMock = new Mock<ITransferVarianceResolver>();
        _varianceResolverMock.Setup(v => v.TryResolveTransferVarianceAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        var logger = Mock.Of<ILogger<StockAdjustmentService>>();
        _service = CreateService(_uow);
    }

    private async Task<(Product product, Warehouse warehouse, User user)> SeedAsync(
        int costPrice = 10,
        int? currentStock = 100,
        int safetyStock = 10,
        int reorderPoint = 20,
        decimal length = 1m,
        decimal width = 1m,
        decimal height = 1m,
        decimal weight = 1m,
        BinType preferredBinType = BinType.Standard)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Hardware", Description = "Tools" };
        var product = new Product
        {
            Id = Guid.NewGuid(), Name = "Widget", SKU = "WGT-001",
            CostPrice = costPrice, SellingPrice = 15m, ReorderPoint = reorderPoint, SafetyStockQty = safetyStock,
            IsActive = true, CategoryId = category.Id, Category = category,
            Length = length, Width = width, Height = height, WeightKg = weight,
            PreferredBinType = preferredBinType
        };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), Name = "WH1", Code = "WH1" };
        var role = await _context.Roles.FirstAsync(r => r.Name == "Staff");
        var user = new User
        {
            Id = Guid.NewGuid(), FullName = "Staff User", Email = "staff@test.com",
            PasswordHash = "hash", IsActive = true, Status = UserStatus.Active, RoleId = role.Id, Role = role
        };

        await _context.Categories.AddAsync(category);
        await _context.Products.AddAsync(product);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.Users.AddAsync(user);

        if (currentStock.HasValue)
        {
            var sl = new StockLevel
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                QuantityOnHand = currentStock.Value,
                QuantityReserved = 0,
                QuantityOnOrder = 0,
                QuantityInTransit = 0,
                LastUpdated = DateTime.UtcNow
            };

            await _context.StockLevels.AddAsync(sl);
        }

        await _context.SaveChangesAsync();

        return (product, warehouse, user);
    }

    private StockAdjustmentService CreateService(IUnitOfWork uow)
        => new(uow, _notificationMock.Object, Mock.Of<ILogger<StockAdjustmentService>>(),
            _currentUserServiceMock.Object, _cacheMock.Object, _publisherMock.Object,
            _authorizationServiceMock.Object, _varianceResolverMock.Object);

    private async Task<(Product product, Warehouse warehouse, User user, WarehouseZone zone, BinLocation bin)> SeedBinScenarioAsync(
        ZoneType zoneType = ZoneType.Receiving,
        BinType binType = BinType.Standard,
        BinType preferredBinType = BinType.Standard,
        decimal binUtilizedVolume = 9m,
        decimal binMaxVolume = 10m,
        int? currentStock = null,
        int costPrice = 1,
        int safetyStock = 0,
        int reorderPoint = 0)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Hardware", Description = "Tools" };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), Name = "WH-CAP", Code = "WH-CAP" };
        var zone = new WarehouseZone
        {
            Id = Guid.NewGuid(),
            Name = "Zone-CAP",
            Code = "ZONE-CAP",
            ZoneType = zoneType,
            WarehouseId = warehouse.Id,
            AreaSqFt = 100,
            MaxVolumeCm3 = 1000,
            MaxWeightKg = 1000
        };
        var bin = new BinLocation
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-CAP",
            ZoneId = zone.Id,
            Zone = zone,
            BinType = binType,
            MaxVolumeCm3 = binMaxVolume,
            MaxWeightKg = 1000,
            UtilizedVolumeCm3 = binUtilizedVolume,
            UtilizedWeightKg = 0
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Cap Widget",
            SKU = "CAP-001",
            CostPrice = costPrice,
            SellingPrice = 15m,
            ReorderPoint = reorderPoint,
            SafetyStockQty = safetyStock,
            IsActive = true,
            CategoryId = category.Id,
            Category = category,
            Length = 1m,
            Width = 1m,
            Height = 1m,
            WeightKg = 1m,
            PreferredBinType = preferredBinType
        };
        var role = await _context.Roles.FirstAsync(r => r.Name == "Staff");
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Cap User",
            Email = "cap@test.com",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = role.Id,
            Role = role
        };

        await _context.Categories.AddAsync(category);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.WarehouseZones.AddAsync(zone);
        await _context.BinLocations.AddAsync(bin);
        await _context.Products.AddAsync(product);
        await _context.Users.AddAsync(user);

        if (currentStock.HasValue)
        {
            await _context.StockLevels.AddAsync(new StockLevel
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                BinLocationId = bin.Id,
                QuantityOnHand = currentStock.Value,
                QuantityReserved = 0,
                QuantityOnOrder = 0,
                QuantityInTransit = 0,
                LastUpdated = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        return (product, warehouse, user, zone, bin);
    }

    private sealed class ThrowingCommitUnitOfWork : IUnitOfWork
    {
        private readonly IUnitOfWork _inner;

        public ThrowingCommitUnitOfWork(IUnitOfWork inner)
        {
            _inner = inner;
        }

        public IProductRepository Products => _inner.Products;
        public ISupplierRepository Suppliers => _inner.Suppliers;
        public IPurchaseOrderRepository PurchaseOrders => _inner.PurchaseOrders;
        public ITransferRepository Transfers => _inner.Transfers;
        public IBarcodeRepository Barcodes => _inner.Barcodes;
        public INotificationRepository Notifications => _inner.Notifications;
        public IStockLevelRepository StockLevels => _inner.StockLevels;

        public IGenericRepository<T> Repository<T>() where T : BaseEntity => _inner.Repository<T>();

        public Task<int> CommitAsync() => throw new DbUpdateConcurrencyException("Simulated concurrency conflict.");

        public void Dispose()
        {
        }
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
        _notificationMock.Verify(n => n.SendPendingStockAdjustmentApprovalAlertAsync(It.IsAny<Guid>()), Times.Once);
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
    public async Task GetAdjustmentByIdAsync_ReturnsStaleWarning_WhenCurrentStockChanged()
    {
        // Arrange: adjustment was created at 100, but live stock is now 90
        var (product, warehouse, user) = await SeedAsync(costPrice: 10, currentStock: 90);
        var role = await _context.Roles.FirstAsync(r => r.Name == "Manager");
        var manager = new User
        {
            Id = Guid.NewGuid(), FullName = "Manager", Email = "mgr-live@test.com",
            PasswordHash = "hash", IsActive = true, Status = UserStatus.Active, RoleId = role.Id, Role = role
        };
        await _context.Users.AddAsync(manager);

        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ-STALE-WARN",
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Reason = AdjustmentReason.CycleCount,
            QuantityBefore = 100,
            QuantityAfter = 80,
            QuantityChange = -20,
            Status = AdjustmentStatus.Pending,
            PerformedBy = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        await _context.StockAdjustments.AddAsync(adjustment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAdjustmentByIdAsync(adjustment.Id);

        // Assert
        Assert.Equal(90, result.CurrentQuantity);
        Assert.True(result.IsStale);
        Assert.Equal("Stock has changed since this adjustment was created.", result.WarningMessage);
        Assert.Equal(100, result.QuantityBefore);
        Assert.Equal(80, result.QuantityAfter);
    }

    [Fact]
    public async Task ApproveAdjustmentAsync_Approve_StillProceeds_WhenStockChanged()
    {
        // Arrange: pending adjustment was created at 100, but current stock is 90
        var (product, warehouse, user) = await SeedAsync(costPrice: 10, currentStock: 90);
        var role = await _context.Roles.FirstAsync(r => r.Name == "Manager");
        var manager = new User
        {
            Id = Guid.NewGuid(), FullName = "Manager", Email = "mgr-liveapprove@test.com",
            PasswordHash = "hash", IsActive = true, Status = UserStatus.Active, RoleId = role.Id, Role = role
        };
        await _context.Users.AddAsync(manager);

        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ-LIVE-APPROVE",
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Reason = AdjustmentReason.CycleCount,
            QuantityBefore = 100,
            QuantityAfter = 80,
            QuantityChange = -20,
            Status = AdjustmentStatus.Pending,
            PerformedBy = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        await _context.StockAdjustments.AddAsync(adjustment);
        await _context.SaveChangesAsync();

        _currentUserServiceMock.Setup(c => c.UserId).Returns(manager.Id);

        // Act
        var result = await _service.ApproveAdjustmentAsync(adjustment.Id, new StockAdjustmentApprovalDto { Approve = true });

        // Assert: approval still happens and updates stock as before
        Assert.Equal(AdjustmentStatus.Approved, result.Status);
        var sl = await _context.StockLevels.FirstAsync(s => s.ProductId == product.Id && s.WarehouseId == warehouse.Id);
        Assert.Equal(80, sl.QuantityOnHand);
    }

    [Fact]
    public async Task ApproveAdjustmentAsync_AdminCanApproveOwnAdjustment()
    {
        // Arrange: admin performs an adjustment, then approves it herself
        var (product, warehouse, user) = await SeedAsync(costPrice: 10, currentStock: 100);
        var role = await _context.Roles.FirstAsync(r => r.Name == "Admin");
        var admin = new User
        {
            Id = Guid.NewGuid(), FullName = "Admin User", Email = "admin@test.com",
            PasswordHash = "hash", IsActive = true, Status = UserStatus.Active, RoleId = role.Id, Role = role
        };
        await _context.Users.AddAsync(admin);

        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ-ADMIN-SELFAPPROVE",
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Reason = AdjustmentReason.CycleCount,
            QuantityBefore = 100,
            QuantityAfter = 110,
            QuantityChange = 10,
            Status = AdjustmentStatus.Pending,
            PerformedBy = admin.Id,
            CreatedAt = DateTime.UtcNow
        };
        await _context.StockAdjustments.AddAsync(adjustment);
        await _context.SaveChangesAsync();

        _currentUserServiceMock.Setup(c => c.UserId).Returns(admin.Id);

        // Act
        var result = await _service.ApproveAdjustmentAsync(adjustment.Id,
            new StockAdjustmentApprovalDto { Approve = true });

        // Assert
        Assert.Equal(AdjustmentStatus.Approved, result.Status);
        Assert.Equal(admin.Id, result.ApprovedBy);

        var sl = await _context.StockLevels
            .FirstAsync(s => s.ProductId == product.Id && s.WarehouseId == warehouse.Id);
        Assert.Equal(110, sl.QuantityOnHand);
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

    [Fact]
    public async Task CreateAdjustmentAsync_ReturnsCachedResponse_WhenIdempotencyKeyHit()
    {
        var cached = new StockAdjustmentResponseDto
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ-CACHED",
            Status = AdjustmentStatus.Approved
        };

        _cacheMock.Setup(c => c.GetAsync<StockAdjustmentResponseDto>("Idempotency_Adj_CACHE-1"))
            .ReturnsAsync(cached);

        var result = await _service.CreateAdjustmentAsync(new StockAdjustmentCreateDto
        {
            ProductId = Guid.NewGuid(),
            WarehouseId = Guid.NewGuid(),
            QuantityAfter = 1,
            Reason = AdjustmentReason.CycleCount,
            PerformedBy = Guid.NewGuid(),
            IdempotencyKey = "CACHE-1"
        });

        Assert.Same(cached, result);
        _cacheMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<StockAdjustmentResponseDto>(), It.IsAny<TimeSpan?>()), Times.Never);
        Assert.Empty(await _context.StockAdjustments.ToListAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task CreateAdjustmentAsync_ValidationFailures_ThrowExpectedExceptions(int scenario)
    {
        switch ((CreateAdjustmentValidationScenario)scenario)
        {
            case CreateAdjustmentValidationScenario.NegativeQuantity:
                await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateAdjustmentAsync(new StockAdjustmentCreateDto
                {
                    ProductId = Guid.NewGuid(),
                    WarehouseId = Guid.NewGuid(),
                    QuantityAfter = -1,
                    Reason = AdjustmentReason.CycleCount,
                    PerformedBy = Guid.NewGuid()
                }));
                break;

            case CreateAdjustmentValidationScenario.MissingProduct:
            {
                var (_, warehouse, user) = await SeedAsync(currentStock: null);
                _currentUserServiceMock.Setup(c => c.UserId).Returns(user.Id);

                var ex = await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAdjustmentAsync(new StockAdjustmentCreateDto
                {
                    ProductId = Guid.NewGuid(),
                    WarehouseId = warehouse.Id,
                    QuantityAfter = 1,
                    Reason = AdjustmentReason.CycleCount,
                    PerformedBy = user.Id
                }));

                Assert.Contains("Product with identifier", ex.Message);
                break;
            }

            case CreateAdjustmentValidationScenario.MissingWarehouse:
            {
                var (product, _, user) = await SeedAsync(currentStock: null);
                _currentUserServiceMock.Setup(c => c.UserId).Returns(user.Id);

                var ex = await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAdjustmentAsync(new StockAdjustmentCreateDto
                {
                    ProductId = product.Id,
                    WarehouseId = Guid.NewGuid(),
                    QuantityAfter = 1,
                    Reason = AdjustmentReason.CycleCount,
                    PerformedBy = user.Id
                }));

                Assert.Contains("Warehouse with identifier", ex.Message);
                break;
            }

            case CreateAdjustmentValidationScenario.MissingUser:
            {
                var (product, warehouse, _) = await SeedAsync(currentStock: null);
                var missingUserId = Guid.NewGuid();
                _currentUserServiceMock.Setup(c => c.UserId).Returns(missingUserId);

                var ex = await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAdjustmentAsync(new StockAdjustmentCreateDto
                {
                    ProductId = product.Id,
                    WarehouseId = warehouse.Id,
                    QuantityAfter = 1,
                    Reason = AdjustmentReason.CycleCount,
                    PerformedBy = missingUserId
                }));

                Assert.Contains("User with identifier", ex.Message);
                break;
            }

            case CreateAdjustmentValidationScenario.MissingBin:
            {
                var (product, warehouse, user) = await SeedAsync(currentStock: null);
                _currentUserServiceMock.Setup(c => c.UserId).Returns(user.Id);
                var missingBinId = Guid.NewGuid();

                var ex = await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAdjustmentAsync(new StockAdjustmentCreateDto
                {
                    ProductId = product.Id,
                    WarehouseId = warehouse.Id,
                    BinLocationId = missingBinId,
                    QuantityAfter = 1,
                    Reason = AdjustmentReason.CycleCount,
                    PerformedBy = user.Id
                }));

                Assert.Contains("BinLocation with identifier", ex.Message);
                break;
            }
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 5)]
    [InlineData(2, 15)]
    public async Task CreateAdjustmentAsync_NewStockLevel_UsesAddPathAndRaisesCorrectStockAlert(
        int scenario,
        int quantityAfter)
    {
        var (product, warehouse, user) = await SeedAsync(costPrice: 1, currentStock: null, safetyStock: 10, reorderPoint: 20);
        _currentUserServiceMock.Setup(c => c.UserId).Returns(user.Id);

        var response = await _service.CreateAdjustmentAsync(new StockAdjustmentCreateDto
        {
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            QuantityAfter = quantityAfter,
            Reason = AdjustmentReason.CycleCount,
            Notes = $"Scenario {scenario}",
            PerformedBy = user.Id,
            IdempotencyKey = $"ALERT-{scenario}"
        });

        Assert.Equal(AdjustmentStatus.Approved, response.Status);

        var stock = await _context.StockLevels.SingleAsync(sl => sl.ProductId == product.Id && sl.WarehouseId == warehouse.Id);
        Assert.Equal(quantityAfter, stock.QuantityOnHand);

        switch ((CreateStockAlertScenario)scenario)
        {
            case CreateStockAlertScenario.OutOfStock:
                _notificationMock.Verify(n => n.SendOutOfStockAlertAsync(product.Id, warehouse.Id, 0), Times.Once);
                _notificationMock.Verify(n => n.SendSafetyStockAlertAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
                _notificationMock.Verify(n => n.SendLowStockAlertAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
                break;

            case CreateStockAlertScenario.SafetyStock:
                _notificationMock.Verify(n => n.SendSafetyStockAlertAsync(product.Id, warehouse.Id, 5, 10), Times.Once);
                _notificationMock.Verify(n => n.SendOutOfStockAlertAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
                _notificationMock.Verify(n => n.SendLowStockAlertAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
                break;

            case CreateStockAlertScenario.LowStock:
                _notificationMock.Verify(n => n.SendLowStockAlertAsync(product.Id, warehouse.Id, 15, 20), Times.Once);
                _notificationMock.Verify(n => n.SendOutOfStockAlertAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
                _notificationMock.Verify(n => n.SendSafetyStockAlertAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
                break;
        }

        _cacheMock.Verify(c => c.SetAsync($"Idempotency_Adj_ALERT-{scenario}", It.IsAny<StockAdjustmentResponseDto>(), It.IsAny<TimeSpan?>()), Times.Once);
    }

    [Fact]
    public async Task CreateAdjustmentAsync_WhenWarningsAreBypassed_RequiresAuthorizationAndPersistsOverrideAudit()
    {
        var (product, warehouse, user, _, bin) = await SeedBinScenarioAsync(
            zoneType: ZoneType.Receiving,
            binType: BinType.Standard,
            preferredBinType: BinType.Standard,
            binUtilizedVolume: 9m,
            binMaxVolume: 10m,
            currentStock: null,
            costPrice: 1);

        _currentUserServiceMock.Setup(c => c.UserId).Returns(user.Id);
        _currentUserServiceMock.Setup(c => c.Principal).Returns(new ClaimsPrincipal(new ClaimsIdentity("test")));

        var dto = new StockAdjustmentCreateDto
        {
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            BinLocationId = bin.Id,
            QuantityAfter = 1,
            Reason = AdjustmentReason.Correction,
            Notes = "Capacity override",
            PerformedBy = user.Id,
            BypassWarnings = false,
            OverrideReason = "Manual override"
        };

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateAdjustmentAsync(dto));
        Assert.Equal(0, await _context.OverrideAuditLogs.CountAsync());

        dto.BypassWarnings = true;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateAdjustmentAsync(dto));
        Assert.Equal(0, await _context.OverrideAuditLogs.CountAsync());

        _authorizationServiceMock
            .Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var result = await _service.CreateAdjustmentAsync(dto);

        Assert.Equal(AdjustmentStatus.Approved, result.Status);
        Assert.Equal(1, await _context.OverrideAuditLogs.CountAsync());

        var audit = await _context.OverrideAuditLogs.SingleAsync();
        Assert.Equal("ZoneMismatch", audit.RuleBroken);
        Assert.Equal("Manual override", audit.OverrideReason);
        Assert.Equal(bin.Id, audit.TargetBinId);
        Assert.Equal(product.Id, audit.ProductId);
        Assert.Equal(user.Id, audit.UserId);

        var updatedBin = await _context.BinLocations.SingleAsync(b => b.Id == bin.Id);
        Assert.Equal(10m, updatedBin.UtilizedVolumeCm3);
        Assert.Equal(1m, updatedBin.UtilizedWeightKg);

        var stock = await _context.StockLevels.SingleAsync(sl => sl.ProductId == product.Id && sl.WarehouseId == warehouse.Id && sl.BinLocationId == bin.Id);
        Assert.Equal(1, stock.QuantityOnHand);
    }

    [Fact]
    public async Task CreateAdjustmentAsync_WhenCommitThrowsConcurrencyException_ThrowsStaleDataException()
    {
        var (product, warehouse, user) = await SeedAsync(costPrice: 1, currentStock: 100);
        _currentUserServiceMock.Setup(c => c.UserId).Returns(user.Id);

        var throwingService = CreateService(new ThrowingCommitUnitOfWork(_uow));

        var ex = await Assert.ThrowsAsync<StaleDataException>(() => throwingService.CreateAdjustmentAsync(new StockAdjustmentCreateDto
        {
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            QuantityAfter = 101,
            Reason = AdjustmentReason.CycleCount,
            PerformedBy = user.Id
        }));

        Assert.Equal(100, ex.CurrentQuantity);
    }

    [Fact]
    public async Task GetAdjustmentByIdAsync_ReturnsNotStaleDetails_WhenCurrentStockMatches()
    {
        var (product, warehouse, user) = await SeedAsync(currentStock: 90);

        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ-FRESH",
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Reason = AdjustmentReason.CycleCount,
            QuantityBefore = 90,
            QuantityAfter = 95,
            QuantityChange = 5,
            Status = AdjustmentStatus.Pending,
            PerformedBy = user.Id,
            CreatedAt = DateTime.UtcNow
        };

        await _context.StockAdjustments.AddAsync(adjustment);
        await _context.SaveChangesAsync();

        var result = await _service.GetAdjustmentByIdAsync(adjustment.Id);

        Assert.Equal(90, result.CurrentQuantity);
        Assert.False(result.IsStale);
        Assert.Null(result.WarningMessage);
    }

    [Fact]
    public async Task GetAdjustmentsAsync_WithAllFilters_MapsTransferNumberAndReturnsSingleMatch()
    {
        var (product, warehouse, user) = await SeedAsync(currentStock: null);
        var managerRole = await _context.Roles.FirstAsync(r => r.Name == "Manager");
        var manager = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Manager User",
            Email = "manager@test.com",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = managerRole.Id,
            Role = managerRole
        };
        var transfer = new WarehouseTransfer
        {
            Id = Guid.NewGuid(),
            TransferNumber = "TRF-9001",
            Status = TransferStatus.Requested,
            FromWarehouseId = warehouse.Id,
            ToWarehouseId = warehouse.Id,
            RequestedBy = user.Id
        };

        var matchingAdjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ-FILTER-9001",
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Reason = AdjustmentReason.LossInTransit,
            ReferenceType = ReferenceType.Transfer,
            ReferenceId = transfer.Id,
            Status = AdjustmentStatus.Pending,
            QuantityBefore = 10,
            QuantityAfter = 7,
            QuantityChange = -3,
            PerformedBy = user.Id,
            PerformedByUser = user,
            ApprovedBy = manager.Id,
            ApprovedByUser = manager,
            CreatedAt = DateTime.UtcNow
        };
        var unrelatedAdjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ-OTHER-1",
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Reason = AdjustmentReason.CycleCount,
            Status = AdjustmentStatus.Approved,
            QuantityBefore = 5,
            QuantityAfter = 5,
            QuantityChange = 0,
            PerformedBy = user.Id,
            PerformedByUser = user,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        await _context.Users.AddAsync(manager);
        await _context.WarehouseTransfers.AddAsync(transfer);
        await _context.StockAdjustments.AddAsync(unrelatedAdjustment);
        await _context.StockAdjustments.AddAsync(matchingAdjustment);
        await _context.SaveChangesAsync();

        var result = await _service.GetAdjustmentsAsync(new StockAdjustmentQueryParameters
        {
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Status = AdjustmentStatus.Pending,
            Reason = AdjustmentReason.LossInTransit,
            ReferenceType = ReferenceType.Transfer,
            ReferenceId = transfer.Id,
            Search = "FILTER",
            Page = 1,
            PageSize = 10
        });

        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Data);
        Assert.Equal(matchingAdjustment.Id, item.Id);
        Assert.Equal("TRF-9001", item.TransferNumber);
        Assert.Equal(ReferenceType.Transfer, item.ReferenceType);
        Assert.Equal(AdjustmentStatus.Pending, item.Status);
    }

    [Fact]
    public async Task GetAdjustmentsAsync_WithNoFilters_LeavesTransferNumberNullForNonTransferRows()
    {
        var (product, warehouse, user) = await SeedAsync(currentStock: null);
        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ-NOFILTER-1",
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Reason = AdjustmentReason.CycleCount,
            Status = AdjustmentStatus.Approved,
            QuantityBefore = 4,
            QuantityAfter = 4,
            QuantityChange = 0,
            PerformedBy = user.Id,
            PerformedByUser = user,
            CreatedAt = DateTime.UtcNow
        };

        await _context.StockAdjustments.AddAsync(adjustment);
        await _context.SaveChangesAsync();

        var result = await _service.GetAdjustmentsAsync(new StockAdjustmentQueryParameters());

        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Data);
        Assert.Equal(adjustment.Id, item.Id);
        Assert.Null(item.TransferNumber);
    }

    [Fact]
    public async Task ApproveAdjustmentAsync_WhenDamageEvidenceIsVerified_CreatesMissingStockLevel()
    {
        var (product, warehouse, performer) = await SeedAsync(currentStock: null);
        var managerRole = await _context.Roles.FirstAsync(r => r.Name == "Manager");
        var manager = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Manager Approver",
            Email = "approver@test.com",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = managerRole.Id,
            Role = managerRole
        };

        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ-DAMAGE-EVIDENCE",
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Reason = AdjustmentReason.Damage,
            QuantityBefore = 5,
            QuantityAfter = 0,
            QuantityChange = -5,
            Status = AdjustmentStatus.Pending,
            PerformedBy = performer.Id,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Users.AddAsync(manager);
        await _context.StockAdjustments.AddAsync(adjustment);
        await _context.FileAttachments.AddAsync(new FileAttachment
        {
            Id = Guid.NewGuid(),
            EntityType = "StockAdjustment",
            EntityId = adjustment.Id,
            FileName = "damage.jpg",
            FilePath = "/tmp/damage.jpg",
            MimeType = "image/jpeg",
            FileSizeBytes = 1,
            Category = DocumentCategory.DamageEvidence,
            IsVerified = true,
            UploadedBy = manager.Id
        });
        await _context.SaveChangesAsync();

        _currentUserServiceMock.Setup(c => c.UserId).Returns(manager.Id);

        var result = await _service.ApproveAdjustmentAsync(adjustment.Id, new StockAdjustmentApprovalDto { Approve = true });

        Assert.Equal(AdjustmentStatus.Approved, result.Status);

        var stock = await _context.StockLevels.SingleAsync(sl => sl.ProductId == product.Id && sl.WarehouseId == warehouse.Id);
        Assert.Equal(0, stock.QuantityOnHand);
    }

    [Fact]
    public async Task ApproveAdjustmentAsync_WhenLossInTransitReferencesTransfer_UpdatesTransitStockAndResolvesVariance()
    {
        var (product, warehouse, performer) = await SeedAsync(currentStock: 13);
        var managerRole = await _context.Roles.FirstAsync(r => r.Name == "Manager");
        var manager = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Manager Approver",
            Email = "loss@test.com",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = managerRole.Id,
            Role = managerRole
        };
        var transfer = new WarehouseTransfer
        {
            Id = Guid.NewGuid(),
            TransferNumber = "TRF-LOSS-1",
            Status = TransferStatus.Requested,
            FromWarehouseId = warehouse.Id,
            ToWarehouseId = warehouse.Id,
            RequestedBy = performer.Id
        };
        var stock = await _context.StockLevels.SingleAsync(sl => sl.ProductId == product.Id && sl.WarehouseId == warehouse.Id);
        stock.QuantityInTransit = 4;
        _context.StockLevels.Update(stock);

        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ-LOSS-1",
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Reason = AdjustmentReason.LossInTransit,
            ReferenceType = ReferenceType.Transfer,
            ReferenceId = transfer.Id,
            QuantityBefore = 13,
            QuantityAfter = 9,
            QuantityChange = -4,
            Status = AdjustmentStatus.Pending,
            PerformedBy = performer.Id,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Users.AddAsync(manager);
        await _context.WarehouseTransfers.AddAsync(transfer);
        await _context.StockAdjustments.AddAsync(adjustment);
        await _context.SaveChangesAsync();

        _currentUserServiceMock.Setup(c => c.UserId).Returns(manager.Id);

        var result = await _service.ApproveAdjustmentAsync(adjustment.Id, new StockAdjustmentApprovalDto { Approve = true });

        Assert.Equal(AdjustmentStatus.Approved, result.Status);
        Assert.Equal("TRF-LOSS-1", result.TransferNumber);

        var updatedStock = await _context.StockLevels.SingleAsync(sl => sl.ProductId == product.Id && sl.WarehouseId == warehouse.Id);
        Assert.Equal(0, updatedStock.QuantityInTransit);
        _varianceResolverMock.Verify(v => v.TryResolveTransferVarianceAsync(transfer.Id), Times.Once);
    }

    [Fact]
    public async Task ApproveAdjustmentAsync_WhenCommitThrowsConcurrencyException_ThrowsStaleDataException()
    {
        var (product, warehouse, performer) = await SeedAsync(currentStock: 100);
        var managerRole = await _context.Roles.FirstAsync(r => r.Name == "Manager");
        var manager = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Manager Approver",
            Email = "concurrency@test.com",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = managerRole.Id,
            Role = managerRole
        };
        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ-CONC-APPROVE",
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Reason = AdjustmentReason.CycleCount,
            QuantityBefore = 100,
            QuantityAfter = 95,
            QuantityChange = -5,
            Status = AdjustmentStatus.Pending,
            PerformedBy = performer.Id,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Users.AddAsync(manager);
        await _context.StockAdjustments.AddAsync(adjustment);
        await _context.SaveChangesAsync();

        _currentUserServiceMock.Setup(c => c.UserId).Returns(manager.Id);

        var throwingService = CreateService(new ThrowingCommitUnitOfWork(_uow));
        var ex = await Assert.ThrowsAsync<StaleDataException>(() => throwingService.ApproveAdjustmentAsync(adjustment.Id, new StockAdjustmentApprovalDto { Approve = true }));

        Assert.Equal(100, ex.CurrentQuantity);
    }

    [Fact]
    public async Task CancelStockAdjustmentAsync_WhenAdjustmentMissing_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CancelStockAdjustmentAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task CancelStockAdjustmentAsync_WhenAdjustmentIsNotApproved_ThrowsBusinessRuleException()
    {
        var (product, warehouse, user) = await SeedAsync(currentStock: 10);
        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ-CANCEL-INVALID",
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Reason = AdjustmentReason.CycleCount,
            QuantityBefore = 10,
            QuantityAfter = 15,
            QuantityChange = 5,
            Status = AdjustmentStatus.Pending,
            PerformedBy = user.Id,
            CreatedAt = DateTime.UtcNow
        };

        await _context.StockAdjustments.AddAsync(adjustment);
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CancelStockAdjustmentAsync(adjustment.Id, user.Id));
        Assert.Contains("Only approved adjustments can be reversed.", ex.Message);
    }

    [Fact]
    public async Task CancelStockAdjustmentAsync_WhenApprovedNonLossAdjustmentReversesStockAndBinCapacity()
    {
        var (product, warehouse, user, _, bin) = await SeedBinScenarioAsync(
            zoneType: ZoneType.Storage,
            binType: BinType.Standard,
            preferredBinType: BinType.Standard,
            binUtilizedVolume: 20m,
            binMaxVolume: 100m,
            currentStock: 13,
            costPrice: 1);

        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ-CANCEL-OK",
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            BinLocationId = bin.Id,
            Reason = AdjustmentReason.Correction,
            QuantityBefore = 10,
            QuantityAfter = 13,
            QuantityChange = 3,
            Status = AdjustmentStatus.Approved,
            PerformedBy = user.Id,
            CreatedAt = DateTime.UtcNow
        };

        await _context.StockAdjustments.AddAsync(adjustment);
        await _context.SaveChangesAsync();

        var result = await _service.CancelStockAdjustmentAsync(adjustment.Id, user.Id);

        Assert.True(result);

        var stock = await _context.StockLevels.SingleAsync(sl => sl.ProductId == product.Id && sl.WarehouseId == warehouse.Id && sl.BinLocationId == bin.Id);
        Assert.Equal(10, stock.QuantityOnHand);

        var updatedBin = await _context.BinLocations.SingleAsync(b => b.Id == bin.Id);
        Assert.Equal(17m, updatedBin.UtilizedVolumeCm3);
    }

    [Fact]
    public async Task CancelStockAdjustmentAsync_WhenApprovedLossInTransitRestoresTransitQuantity()
    {
        var (product, warehouse, user, _, bin) = await SeedBinScenarioAsync(
            zoneType: ZoneType.Storage,
            binType: BinType.Standard,
            preferredBinType: BinType.Standard,
            binUtilizedVolume: 10m,
            binMaxVolume: 100m,
            currentStock: 13,
            costPrice: 1);

        var stock = await _context.StockLevels.SingleAsync(sl => sl.ProductId == product.Id && sl.WarehouseId == warehouse.Id && sl.BinLocationId == bin.Id);
        stock.QuantityInTransit = 4;
        _context.StockLevels.Update(stock);

        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ-CANCEL-LOSS",
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            BinLocationId = bin.Id,
            Reason = AdjustmentReason.LossInTransit,
            QuantityBefore = 13,
            QuantityAfter = 9,
            QuantityChange = -4,
            Status = AdjustmentStatus.Approved,
            PerformedBy = user.Id,
            CreatedAt = DateTime.UtcNow
        };

        await _context.StockAdjustments.AddAsync(adjustment);
        await _context.SaveChangesAsync();

        var result = await _service.CancelStockAdjustmentAsync(adjustment.Id, user.Id);

        Assert.True(result);

        var updatedStock = await _context.StockLevels.SingleAsync(sl => sl.ProductId == product.Id && sl.WarehouseId == warehouse.Id && sl.BinLocationId == bin.Id);
        Assert.Equal(8, updatedStock.QuantityInTransit);

        var updatedBin = await _context.BinLocations.SingleAsync(b => b.Id == bin.Id);
        Assert.Equal(14m, updatedBin.UtilizedVolumeCm3);
    }

    [Fact]
    public async Task CancelStockAdjustmentAsync_WhenApprovedAdjustmentHasNoStockLevel_ReturnsTrueWithoutMutation()
    {
        var (product, warehouse, user) = await SeedAsync(currentStock: null);
        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ-CANCEL-NOSTOCK",
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Reason = AdjustmentReason.CycleCount,
            QuantityBefore = 0,
            QuantityAfter = 5,
            QuantityChange = 5,
            Status = AdjustmentStatus.Approved,
            PerformedBy = user.Id,
            CreatedAt = DateTime.UtcNow
        };

        await _context.StockAdjustments.AddAsync(adjustment);
        await _context.SaveChangesAsync();

        var result = await _service.CancelStockAdjustmentAsync(adjustment.Id, user.Id);

        Assert.True(result);

        var cancelled = await _context.StockAdjustments.SingleAsync(a => a.Id == adjustment.Id);
        Assert.Equal(AdjustmentStatus.Cancelled, cancelled.Status);
        Assert.Empty(await _context.StockLevels.ToListAsync());
    }

    [Fact]
    public async Task CancelStockAdjustmentAsync_WhenReversalWouldGoNegative_ThrowsInsufficientStockException()
    {
        var (product, warehouse, user) = await SeedAsync(currentStock: 1);
        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ-CANCEL-NEGATIVE",
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Reason = AdjustmentReason.Correction,
            QuantityBefore = 1,
            QuantityAfter = 6,
            QuantityChange = 5,
            Status = AdjustmentStatus.Approved,
            PerformedBy = user.Id,
            CreatedAt = DateTime.UtcNow
        };

        await _context.StockAdjustments.AddAsync(adjustment);
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<InsufficientStockException>(() => _service.CancelStockAdjustmentAsync(adjustment.Id, user.Id));

        var stock = await _context.StockLevels.AsNoTracking().SingleAsync(sl => sl.ProductId == product.Id && sl.WarehouseId == warehouse.Id);
        Assert.Equal(1, stock.QuantityOnHand);
        var persistedAdjustment = await _context.StockAdjustments.AsNoTracking().SingleAsync(a => a.Id == adjustment.Id);
        Assert.Equal(AdjustmentStatus.Approved, persistedAdjustment.Status);
    }

    [Fact]
    public async Task CancelStockAdjustmentAsync_WhenCommitThrowsConcurrencyException_ThrowsStaleDataException()
    {
        var (product, warehouse, user) = await SeedAsync(currentStock: 10);
        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ-CANCEL-CONC",
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Reason = AdjustmentReason.CycleCount,
            QuantityBefore = 10,
            QuantityAfter = 12,
            QuantityChange = 2,
            Status = AdjustmentStatus.Approved,
            PerformedBy = user.Id,
            CreatedAt = DateTime.UtcNow
        };

        await _context.StockAdjustments.AddAsync(adjustment);
        await _context.SaveChangesAsync();

        var throwingService = CreateService(new ThrowingCommitUnitOfWork(_uow));
        var ex = await Assert.ThrowsAsync<StaleDataException>(() => throwingService.CancelStockAdjustmentAsync(adjustment.Id, user.Id));

        Assert.Null(ex.CurrentQuantity);
    }

    private enum CreateAdjustmentValidationScenario
    {
        NegativeQuantity = 0,
        MissingProduct = 1,
        MissingWarehouse = 2,
        MissingUser = 3,
        MissingBin = 4
    }

    private enum CreateStockAlertScenario
    {
        OutOfStock = 0,
        SafetyStock = 1,
        LowStock = 2
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
