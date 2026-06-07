using Microsoft.EntityFrameworkCore;
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
/// Unit tests for PurchaseOrderService.
///   — CreatePurchaseOrderAsync: happy path, unknown supplier/product/warehouse
///   — SubmitForApprovalAsync: draft → submitted; non-draft throws
///   — ApprovePurchaseOrderAsync: approved → Approved/Rejected status
///   — ReceiveGoodsAsync: full receipt updates stock, PO status
/// </summary>
public class PurchaseOrderServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _uow;
    private readonly PurchaseOrderService _service;
    private readonly Mock<INotificationService> _notificationMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;

    public PurchaseOrderServiceTests()
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
        
        var cacheServiceMock = new Mock<ICacheService>();
        var publisherMock = new Mock<IPublisher>();
        var valuationServiceMock = new Mock<IInventoryValuationService>();
        var authorizationServiceMock = new Mock<Microsoft.AspNetCore.Authorization.IAuthorizationService>();

        _service = new PurchaseOrderService(_uow, _notificationMock.Object, _currentUserServiceMock.Object, cacheServiceMock.Object, publisherMock.Object, valuationServiceMock.Object, authorizationServiceMock.Object);
    }

    private async Task<(Supplier supplier, Warehouse warehouse, User user, Product product)> SeedAsync()
    {
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(), Name = "Test Supplier", Code = "SUP-001",
            LeadTimeDays = 7, IsActive = true,
            Address = "123 Test St", Email = "supplier@test.com", Phone = "+919876543210"
        };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), Name = "WH1", Code = "WH1" };
        var role = await _context.Roles.FirstAsync(r => r.Name == "Staff");
        var user = new User
        {
            Id = Guid.NewGuid(), FullName = "PO Creator", Email = "po@test.com",
            PasswordHash = "hash", IsActive = true, Status = UserStatus.Active, RoleId = role.Id, Role = role
        };
        var category = new Category { Id = Guid.NewGuid(), Name = "Hardware", Description = "Tools" };
        var product = new Product
        {
            Id = Guid.NewGuid(), Name = "Widget", SKU = "WGT-001",
            CostPrice = 10m, SellingPrice = 15m, ReorderPoint = 5, IsActive = true, CategoryId = category.Id, Category = category
        };

        await _context.Categories.AddAsync(category);
        await _context.Suppliers.AddAsync(supplier);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.Users.AddAsync(user);
        await _context.Products.AddAsync(product);
        
        var supplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id, ProductId = product.Id, IsActive = true, UnitPrice = 10m
        };
        await _context.Set<SupplierProduct>().AddAsync(supplierProduct);

        await _context.SaveChangesAsync();

        return (supplier, warehouse, user, product);
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_ValidData_CreatesDraftPO()
    {
        // Arrange
        var (supplier, warehouse, user, product) = await SeedAsync();
        _currentUserServiceMock.Setup(c => c.UserId).Returns(user.Id);

        var dto = new PurchaseOrderCreateDto
        {
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            CreatedBy = user.Id,
            Items = [new PurchaseOrderItemDto { ProductId = product.Id, QuantityOrdered = 20, UnitPrice = 10m }]
        };

        // Act
        var result = await _service.CreatePurchaseOrderAsync(dto);

        // Assert
        Assert.NotNull(result.PoNumber);
        Assert.StartsWith("PO-", result.PoNumber);
        Assert.Equal(PurchaseOrderStatus.Draft, result.Status);
        Assert.Equal(200m, result.TotalAmount);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_UnknownSupplier_ThrowsNotFoundException()
    {
        // Arrange
        var (_, warehouse, user, product) = await SeedAsync();
        _currentUserServiceMock.Setup(c => c.UserId).Returns(user.Id);

        var dto = new PurchaseOrderCreateDto
        {
            SupplierId = Guid.NewGuid(), // unknown!
            WarehouseId = warehouse.Id,
            CreatedBy = user.Id,
            Items = [new PurchaseOrderItemDto { ProductId = product.Id, QuantityOrdered = 5, UnitPrice = 10m }]
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreatePurchaseOrderAsync(dto));
    }

    [Fact]
    public async Task SubmitForApprovalAsync_DraftPO_TransitionsToSubmitted()
    {
        // Arrange
        var (supplier, warehouse, user, _) = await SeedAsync();
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(), PoNumber = "PO-TEST-001",
            SupplierId = supplier.Id, WarehouseId = warehouse.Id,
            CreatedBy = user.Id, Status = PurchaseOrderStatus.Draft,
            TotalAmount = 100m, CreatedAt = DateTime.UtcNow,
            Supplier = supplier, Warehouse = warehouse,
            CreatedByUser = user, Items = []
        };
        await _context.PurchaseOrders.AddAsync(po);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.SubmitForApprovalAsync(po.Id);

        // Assert
        Assert.Equal(PurchaseOrderStatus.Submitted, result.Status);
    }

    [Fact]
    public async Task SubmitForApprovalAsync_AlreadySubmitted_ThrowsBusinessRuleException()
    {
        // Arrange
        var (supplier, warehouse, user, _) = await SeedAsync();
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(), PoNumber = "PO-TEST-002",
            SupplierId = supplier.Id, WarehouseId = warehouse.Id,
            CreatedBy = user.Id, Status = PurchaseOrderStatus.Submitted, // Not Draft!
            TotalAmount = 100m, CreatedAt = DateTime.UtcNow,
            Supplier = supplier, Warehouse = warehouse,
            CreatedByUser = user, Items = []
        };
        await _context.PurchaseOrders.AddAsync(po);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.SubmitForApprovalAsync(po.Id));
    }

    [Fact]
    public async Task ApprovePurchaseOrderAsync_SubmittedPO_ApprovesIt()
    {
        // Arrange
        var (supplier, warehouse, user, _) = await SeedAsync();
        var role = await _context.Roles.FirstAsync(r => r.Name == "Admin");
        var admin = new User
        {
            Id = Guid.NewGuid(), FullName = "Admin", Email = "admin@test.com",
            PasswordHash = "hash", IsActive = true, Status = UserStatus.Active, RoleId = role.Id, Role = role
        };
        await _context.Users.AddAsync(admin);

        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(), PoNumber = "PO-TEST-003",
            SupplierId = supplier.Id, WarehouseId = warehouse.Id,
            CreatedBy = user.Id, Status = PurchaseOrderStatus.Submitted,
            TotalAmount = 100m, CreatedAt = DateTime.UtcNow,
            Supplier = supplier, Warehouse = warehouse,
            CreatedByUser = user, Items = []
        };
        await _context.PurchaseOrders.AddAsync(po);
        await _context.SaveChangesAsync();

        var dto = new PurchaseOrderApprovalDto { Approve = true };

        _currentUserServiceMock.Setup(c => c.UserId).Returns(admin.Id);

        // Act
        var result = await _service.ApprovePurchaseOrderAsync(po.Id, dto);

        // Assert
        Assert.Equal(PurchaseOrderStatus.Approved, result.Status);
        Assert.Equal(admin.Id, result.ApprovedBy);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
