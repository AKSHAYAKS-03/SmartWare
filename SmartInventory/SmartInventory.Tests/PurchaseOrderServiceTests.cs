using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Security.Claims;
using Moq;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Events;
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
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IPublisher> _publisherMock;
    private readonly Mock<IInventoryValuationService> _valuationServiceMock;
    private readonly Mock<Microsoft.AspNetCore.Authorization.IAuthorizationService> _authorizationServiceMock;

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
        _notificationMock.Setup(n => n.SendOutOfStockAlertAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        _notificationMock.Setup(n => n.SendPurchaseOrderSubmittedAlertAsync(
            It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        _notificationMock.Setup(n => n.SendGoodsReceiptVarianceAlertAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<decimal>()))
            .Returns(Task.CompletedTask);
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _currentUserServiceMock.Setup(c => c.UserId).Returns(Guid.NewGuid()); // Default
        _currentUserServiceMock.Setup(c => c.Principal).Returns((ClaimsPrincipal?)null);
        
        _cacheServiceMock = new Mock<ICacheService>();
        _cacheServiceMock.Setup(c => c.GetAsync<Guid?>(It.IsAny<string>())).ReturnsAsync((Guid?)null);
        _cacheServiceMock.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);

        _publisherMock = new Mock<IPublisher>();
        _publisherMock.Setup(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _valuationServiceMock = new Mock<IInventoryValuationService>();
        _valuationServiceMock.Setup(v => v.RecalculateWacAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<decimal>()))
            .Returns(Task.CompletedTask);

        _authorizationServiceMock = new Mock<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
        _authorizationServiceMock.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync(Microsoft.AspNetCore.Authorization.AuthorizationResult.Success());

        _service = new PurchaseOrderService(
            _uow,
            _notificationMock.Object,
            _currentUserServiceMock.Object,
            _cacheServiceMock.Object,
            _publisherMock.Object,
            _valuationServiceMock.Object,
            _authorizationServiceMock.Object);
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
        _notificationMock.Verify(n => n.SendPurchaseOrderSubmittedAlertAsync(po.Id), Times.Once);
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

    [Fact]
    public async Task UpdatePurchaseOrderAsync_DraftPurchaseOrder_ReplacesItemsAndRecalculatesTotal()
    {
        // Arrange
        var (supplier, warehouse, user, product) = await SeedAsync();
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = "PO-TEST-004",
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            CreatedBy = user.Id,
            Status = PurchaseOrderStatus.Draft,
            TotalAmount = 20m,
            CreatedAt = DateTime.UtcNow,
            Supplier = supplier,
            Warehouse = warehouse,
            CreatedByUser = user,
            Items =
            [
                new PurchaseOrderItem
                {
                    Id = Guid.NewGuid(),
                    PurchaseOrderId = Guid.Empty,
                    ProductId = product.Id,
                    QuantityOrdered = 2,
                    QuantityReceived = 0,
                    UnitPrice = 10m,
                    TotalPrice = 20m
                }
            ]
        };
        po.Items.Single().PurchaseOrderId = po.Id;

        await _context.PurchaseOrders.AddAsync(po);
        await _context.SaveChangesAsync();

        var dto = new PurchaseOrderUpdateDto
        {
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            Notes = "Updated notes",
            Items =
            [
                new PurchaseOrderItemDto
                {
                    ProductId = product.Id,
                    QuantityOrdered = 5,
                    UnitPrice = 10m
                }
            ]
        };

        // Act
        var result = await _service.UpdatePurchaseOrderAsync(po.Id, dto);

        // Assert
        Assert.Equal(PurchaseOrderStatus.Draft, result.Status);
        Assert.Equal(50m, result.TotalAmount);
        Assert.Equal("Updated notes", result.Notes);
        Assert.Single(result.Items);
        Assert.Equal(5, result.Items[0].QuantityOrdered);
        Assert.Equal(1, await _context.PurchaseOrderItems.CountAsync());
    }

    [Fact]
    public async Task UpdatePurchaseOrderAsync_NonDraftPurchaseOrder_ThrowsBusinessRuleException()
    {
        // Arrange
        var (supplier, warehouse, user, product) = await SeedAsync();
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = "PO-TEST-005",
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            CreatedBy = user.Id,
            Status = PurchaseOrderStatus.Submitted,
            TotalAmount = 20m,
            CreatedAt = DateTime.UtcNow,
            Supplier = supplier,
            Warehouse = warehouse,
            CreatedByUser = user,
            Items =
            [
                new PurchaseOrderItem
                {
                    Id = Guid.NewGuid(),
                    PurchaseOrderId = Guid.NewGuid(),
                    ProductId = product.Id,
                    QuantityOrdered = 2,
                    QuantityReceived = 0,
                    UnitPrice = 10m,
                    TotalPrice = 20m
                }
            ]
        };
        po.Items.Single().PurchaseOrderId = po.Id;

        await _context.PurchaseOrders.AddAsync(po);
        await _context.SaveChangesAsync();

        var dto = new PurchaseOrderUpdateDto
        {
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            Items =
            [
                new PurchaseOrderItemDto
                {
                    ProductId = product.Id,
                    QuantityOrdered = 3,
                    UnitPrice = 10m
                }
            ]
        };

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.UpdatePurchaseOrderAsync(po.Id, dto));
    }

    [Fact]
    public async Task ApprovePurchaseOrderAsync_RejectedSubmittedPO_PublishesRejectedEvent()
    {
        // Arrange
        var (supplier, warehouse, user, _) = await SeedAsync();
        var role = await _context.Roles.FirstAsync(r => r.Name == "Admin");
        var approver = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Rejecting Manager",
            Email = "reject@test.com",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = role.Id,
            Role = role
        };

        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = "PO-TEST-006",
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            CreatedBy = user.Id,
            Status = PurchaseOrderStatus.Submitted,
            TotalAmount = 100m,
            CreatedAt = DateTime.UtcNow,
            Supplier = supplier,
            Warehouse = warehouse,
            CreatedByUser = user,
            Items = []
        };

        await _context.Users.AddAsync(approver);
        await _context.PurchaseOrders.AddAsync(po);
        await _context.SaveChangesAsync();

        _currentUserServiceMock.Setup(c => c.UserId).Returns(approver.Id);

        // Act
        var result = await _service.ApprovePurchaseOrderAsync(po.Id, new PurchaseOrderApprovalDto { Approve = false });

        // Assert
        Assert.Equal(PurchaseOrderStatus.Rejected, result.Status);
        Assert.Equal(approver.Id, result.ApprovedBy);
        _publisherMock.Verify(p => p.Publish(
            It.Is<PurchaseOrderRejectedEvent>(e =>
                e.PurchaseOrderId == po.Id &&
                e.PoNumber == po.PoNumber &&
                e.CreatedBy == user.Id &&
                e.ApproverName == approver.FullName),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelPurchaseOrderAsync_DraftPurchaseOrder_CancelsIt()
    {
        // Arrange
        var (supplier, warehouse, user, _) = await SeedAsync();
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = "PO-TEST-007",
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            CreatedBy = user.Id,
            Status = PurchaseOrderStatus.Draft,
            TotalAmount = 100m,
            Notes = "Needs cancel",
            CreatedAt = DateTime.UtcNow,
            Supplier = supplier,
            Warehouse = warehouse,
            CreatedByUser = user,
            Items = []
        };

        await _context.PurchaseOrders.AddAsync(po);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CancelPurchaseOrderAsync(po.Id, user.Id);

        // Assert
        Assert.Equal(PurchaseOrderStatus.Cancelled, result.Status);
        Assert.Contains("Cancelled on", result.Notes!);
        Assert.Equal(result.Notes, (await _context.PurchaseOrders.SingleAsync(x => x.Id == po.Id)).Notes);
    }

    [Theory]
    [InlineData(false, 6, 0, 6, 0, 6, PurchaseOrderStatus.Received)]
    [InlineData(true, 4, 6, 10, 3, 10, PurchaseOrderStatus.PartiallyReceived)]
    public async Task ReceiveGoodsAsync_UpdatesStockAndPOStatus(
        bool existingStock,
        int acceptedQty,
        int startingStock,
        int orderedQty,
        int quantityRejected,
        int expectedStockOnHand,
        PurchaseOrderStatus expectedStatus)
    {
        // Arrange
        var category = new Category { Id = Guid.NewGuid(), Name = "Receiving", Description = "Receiving category" };
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = "Receipt Supplier",
            Code = "SUP-RCV",
            LeadTimeDays = 5,
            IsActive = true,
            Address = "123 Receipt St",
            Email = "receipt@supplier.test",
            Phone = "+911234567890"
        };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), Name = "Receiving WH", Code = "WH-RCV" };
        var role = await _context.Roles.FirstAsync(r => r.Name == "Staff");
        var creator = new User
        {
            Id = Guid.NewGuid(),
            FullName = "PO Creator",
            Email = "creator@receipt.test",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = role.Id,
            Role = role
        };
        var receiver = new User
        {
            Id = Guid.NewGuid(),
            FullName = "GRN Receiver",
            Email = "receiver@receipt.test",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = role.Id,
            Role = role
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Receipt Widget",
            SKU = "RCV-001",
            CostPrice = 10m,
            SellingPrice = 15m,
            ReorderPoint = 20,
            SafetyStockQty = 10,
            IsActive = true,
            CategoryId = category.Id,
            Category = category,
            Length = 1m,
            Width = 1m,
            Height = 1m,
            WeightKg = 1m,
            PreferredBinType = BinType.Standard
        };
        var zone = new WarehouseZone
        {
            Id = Guid.NewGuid(),
            Name = "Storage Zone",
            Code = "ZONE-RCV",
            ZoneType = ZoneType.Storage,
            WarehouseId = warehouse.Id,
            AreaSqFt = 100,
            MaxVolumeCm3 = 10000,
            MaxWeightKg = 10000
        };
        var bin = new BinLocation
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-RCV",
            Barcode = "BIN-BC-RCV",
            ZoneId = zone.Id,
            Zone = zone,
            BinType = BinType.Standard,
            MaxVolumeCm3 = 1000m,
            MaxWeightKg = 1000m,
            UtilizedVolumeCm3 = 0m,
            UtilizedWeightKg = 0m
        };
        var attachment = new FileAttachment
        {
            Id = Guid.NewGuid(),
            EntityType = "PurchaseOrder",
            EntityId = Guid.NewGuid(),
            FileName = "challan.pdf",
            FilePath = "/tmp/challan.pdf",
            MimeType = "application/pdf",
            FileSizeBytes = 1,
            Category = DocumentCategory.DeliveryChallan,
            UploadedBy = creator.Id
        };
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = "PO-TEST-008",
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            CreatedBy = creator.Id,
            Status = PurchaseOrderStatus.Approved,
            TotalAmount = orderedQty * 10m,
            CreatedAt = DateTime.UtcNow,
            Supplier = supplier,
            Warehouse = warehouse,
            CreatedByUser = creator,
            Items = []
        };
        var poItem = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = po.Id,
            ProductId = product.Id,
            Product = product,
            QuantityOrdered = orderedQty,
            QuantityReceived = 0,
            UnitPrice = 10m,
            TotalPrice = orderedQty * 10m
        };
        po.Items.Add(poItem);

        await _context.Categories.AddAsync(category);
        await _context.Suppliers.AddAsync(supplier);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.Users.AddAsync(creator);
        await _context.Users.AddAsync(receiver);
        await _context.Products.AddAsync(product);
        await _context.WarehouseZones.AddAsync(zone);
        await _context.BinLocations.AddAsync(bin);
        await _context.FileAttachments.AddAsync(attachment);
        await _context.PurchaseOrders.AddAsync(po);
        await _context.SaveChangesAsync();

        if (existingStock)
        {
            await _context.StockLevels.AddAsync(new StockLevel
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                BinLocationId = bin.Id,
                QuantityOnHand = startingStock,
                QuantityReserved = 0,
                QuantityOnOrder = 0,
                LastUpdated = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        var dto = new GoodsReceiptCreateDto
        {
            PurchaseOrderId = po.Id,
            ReceivedBy = receiver.Id,
            WarehouseId = warehouse.Id,
            Notes = "Receiving test",
            BypassWarnings = false,
            AttachmentIds = [attachment.Id],
            Items =
            [
                new GoodsReceiptItemDto
                {
                    PurchaseOrderItemId = poItem.Id,
                    BinLocationId = bin.Id,
                    QuantityReceived = acceptedQty + quantityRejected,
                    QuantityRejected = quantityRejected
                }
            ]
        };

        // Act
        var result = await _service.ReceiveGoodsAsync(dto);

        // Assert
        var updatedPo = await _context.PurchaseOrders.Include(p => p.GoodsReceipts).SingleAsync(p => p.Id == po.Id);
        Assert.Equal(expectedStatus, updatedPo.Status);
        Assert.NotEmpty(result.GrnNumber);
        Assert.Single(result.Items);
        Assert.Equal(bin.Barcode, result.Items[0].BinLocationCode);

        var stock = await _context.StockLevels.SingleAsync(sl =>
            sl.ProductId == product.Id &&
            sl.WarehouseId == warehouse.Id &&
            sl.BinLocationId == bin.Id);
        Assert.Equal(expectedStockOnHand, stock.QuantityOnHand);

        Assert.Single(updatedPo.GoodsReceipts);
        Assert.Equal(acceptedQty + quantityRejected, updatedPo.Items.Single().QuantityReceived);
        _valuationServiceMock.Verify(v => v.RecalculateWacAsync(product.Id, acceptedQty, 10m), Times.Once);
    }

    [Fact]
    public async Task ReceiveGoodsAsync_WhenBinCapacityIsExceeded_ThrowsBusinessRuleException()
    {
        // Arrange
        var category = new Category { Id = Guid.NewGuid(), Name = "Receiving", Description = "Receiving category" };
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = "Capacity Supplier",
            Code = "SUP-CAP",
            LeadTimeDays = 5,
            IsActive = true,
            Address = "123 Capacity St",
            Email = "capacity@supplier.test",
            Phone = "+911234567890"
        };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), Name = "Capacity WH", Code = "WH-CAP" };
        var role = await _context.Roles.FirstAsync(r => r.Name == "Staff");
        var creator = new User
        {
            Id = Guid.NewGuid(),
            FullName = "PO Creator",
            Email = "creator@capacity.test",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = role.Id,
            Role = role
        };
        var receiver = new User
        {
            Id = Guid.NewGuid(),
            FullName = "GRN Receiver",
            Email = "receiver@capacity.test",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = role.Id,
            Role = role
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Capacity Widget",
            SKU = "CAP-001",
            CostPrice = 10m,
            SellingPrice = 15m,
            ReorderPoint = 20,
            SafetyStockQty = 10,
            IsActive = true,
            CategoryId = category.Id,
            Category = category,
            Length = 1m,
            Width = 1m,
            Height = 1m,
            WeightKg = 1m,
            PreferredBinType = BinType.Standard
        };
        var zone = new WarehouseZone
        {
            Id = Guid.NewGuid(),
            Name = "Storage Zone",
            Code = "ZONE-CAP",
            ZoneType = ZoneType.Storage,
            WarehouseId = warehouse.Id,
            AreaSqFt = 100,
            MaxVolumeCm3 = 10000,
            MaxWeightKg = 10000
        };
        var bin = new BinLocation
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-CAP",
            Barcode = "BIN-BC-CAP",
            ZoneId = zone.Id,
            Zone = zone,
            BinType = BinType.Standard,
            MaxVolumeCm3 = 2m,
            MaxWeightKg = 1000m,
            UtilizedVolumeCm3 = 1m,
            UtilizedWeightKg = 0m
        };
        var attachment = new FileAttachment
        {
            Id = Guid.NewGuid(),
            EntityType = "PurchaseOrder",
            EntityId = Guid.NewGuid(),
            FileName = "challan.pdf",
            FilePath = "/tmp/challan.pdf",
            MimeType = "application/pdf",
            FileSizeBytes = 1,
            Category = DocumentCategory.DeliveryChallan,
            UploadedBy = creator.Id
        };
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = "PO-TEST-009",
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            CreatedBy = creator.Id,
            Status = PurchaseOrderStatus.Approved,
            TotalAmount = 50m,
            CreatedAt = DateTime.UtcNow,
            Supplier = supplier,
            Warehouse = warehouse,
            CreatedByUser = creator,
            Items = []
        };
        var poItem = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = po.Id,
            ProductId = product.Id,
            Product = product,
            QuantityOrdered = 10,
            QuantityReceived = 0,
            UnitPrice = 10m,
            TotalPrice = 50m
        };
        po.Items.Add(poItem);

        await _context.Categories.AddAsync(category);
        await _context.Suppliers.AddAsync(supplier);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.Users.AddAsync(creator);
        await _context.Users.AddAsync(receiver);
        await _context.Products.AddAsync(product);
        await _context.WarehouseZones.AddAsync(zone);
        await _context.BinLocations.AddAsync(bin);
        await _context.FileAttachments.AddAsync(attachment);
        await _context.PurchaseOrders.AddAsync(po);
        await _context.SaveChangesAsync();

        var dto = new GoodsReceiptCreateDto
        {
            PurchaseOrderId = po.Id,
            ReceivedBy = receiver.Id,
            WarehouseId = warehouse.Id,
            AttachmentIds = [attachment.Id],
            Items =
            [
                new GoodsReceiptItemDto
                {
                    PurchaseOrderItemId = poItem.Id,
                    BinLocationId = bin.Id,
                    QuantityReceived = 3,
                    QuantityRejected = 0
                }
            ]
        };

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ReceiveGoodsAsync(dto));
    }

    [Fact]
    public async Task ReceiveGoodsByBarcodeAsync_ValidScan_UsesBarcodeToReceiveGoods()
    {
        // Arrange
        var category = new Category { Id = Guid.NewGuid(), Name = "Barcode", Description = "Barcode category" };
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = "Barcode Supplier",
            Code = "SUP-BC",
            LeadTimeDays = 5,
            IsActive = true,
            Address = "123 Barcode St",
            Email = "barcode@supplier.test",
            Phone = "+911234567890"
        };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), Name = "Barcode WH", Code = "WH-BC" };
        var role = await _context.Roles.FirstAsync(r => r.Name == "Staff");
        var creator = new User
        {
            Id = Guid.NewGuid(),
            FullName = "PO Creator",
            Email = "creator@barcode.test",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = role.Id,
            Role = role
        };
        var receiver = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Barcode Receiver",
            Email = "receiver@barcode.test",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = role.Id,
            Role = role
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Barcode Widget",
            SKU = "BC-001",
            CostPrice = 10m,
            SellingPrice = 15m,
            ReorderPoint = 20,
            SafetyStockQty = 10,
            IsActive = true,
            CategoryId = category.Id,
            Category = category,
            Length = 1m,
            Width = 1m,
            Height = 1m,
            WeightKg = 1m,
            PreferredBinType = BinType.Standard
        };
        var barcode = new Barcode
        {
            Id = Guid.NewGuid(),
            BarcodeValue = "BC-001-001",
            BarcodeType = BarcodeType.Code128,
            IsPrimary = true,
            ProductId = product.Id,
            Product = product
        };
        var zone = new WarehouseZone
        {
            Id = Guid.NewGuid(),
            Name = "Storage Zone",
            Code = "ZONE-BC",
            ZoneType = ZoneType.Storage,
            WarehouseId = warehouse.Id,
            AreaSqFt = 100,
            MaxVolumeCm3 = 10000,
            MaxWeightKg = 10000
        };
        var bin = new BinLocation
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-BC",
            Barcode = "BIN-SCAN-001",
            ZoneId = zone.Id,
            Zone = zone,
            BinType = BinType.Standard,
            MaxVolumeCm3 = 1000m,
            MaxWeightKg = 1000m
        };
        var attachment = new FileAttachment
        {
            Id = Guid.NewGuid(),
            EntityType = "PurchaseOrder",
            EntityId = Guid.NewGuid(),
            FileName = "challan.pdf",
            FilePath = "/tmp/challan.pdf",
            MimeType = "application/pdf",
            FileSizeBytes = 1,
            Category = DocumentCategory.DeliveryChallan,
            UploadedBy = creator.Id
        };
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = "PO-TEST-010",
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            CreatedBy = creator.Id,
            Status = PurchaseOrderStatus.Approved,
            TotalAmount = 20m,
            CreatedAt = DateTime.UtcNow,
            Supplier = supplier,
            Warehouse = warehouse,
            CreatedByUser = creator,
            Items = []
        };
        var poItem = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = po.Id,
            ProductId = product.Id,
            Product = product,
            QuantityOrdered = 2,
            QuantityReceived = 0,
            UnitPrice = 10m,
            TotalPrice = 20m
        };
        po.Items.Add(poItem);

        await _context.Categories.AddAsync(category);
        await _context.Suppliers.AddAsync(supplier);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.Users.AddAsync(creator);
        await _context.Users.AddAsync(receiver);
        await _context.Products.AddAsync(product);
        await _context.Barcodes.AddAsync(barcode);
        await _context.WarehouseZones.AddAsync(zone);
        await _context.BinLocations.AddAsync(bin);
        await _context.FileAttachments.AddAsync(attachment);
        await _context.PurchaseOrders.AddAsync(po);
        await _context.SaveChangesAsync();

        var dto = new BarcodeGoodsReceiptCreateDto
        {
            PurchaseOrderId = po.Id,
            ReceivedBy = receiver.Id,
            WarehouseId = warehouse.Id,
            AttachmentIds = [attachment.Id],
            Items =
            [
                new BarcodeGoodsReceiptItemDto
                {
                    BarcodeValue = barcode.BarcodeValue,
                    BinBarcode = bin.Barcode!,
                    QuantityReceived = 2,
                    QuantityRejected = 0
                }
            ]
        };

        // Act
        var result = await _service.ReceiveGoodsByBarcodeAsync(dto);

        // Assert
        Assert.Equal(PurchaseOrderStatus.Received, (await _context.PurchaseOrders.SingleAsync(p => p.Id == po.Id)).Status);
        Assert.NotEmpty(result.GrnNumber);
        Assert.Single(result.Items);
        Assert.Equal(bin.Id, result.Items[0].BinLocationId);
        Assert.Equal(product.Id, result.Items[0].ProductId);
        Assert.Equal(2, (await _context.StockLevels.SingleAsync()).QuantityOnHand);
    }

    [Fact]
    public async Task CancelGoodsReceiptAsync_AfterAcceptedReceipt_RevertsStockAndRestoresPurchaseOrderStatus()
    {
        // Arrange
        var category = new Category { Id = Guid.NewGuid(), Name = "Cancel GRN", Description = "Cancel GRN category" };
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = "Cancel Supplier",
            Code = "SUP-CGRN",
            LeadTimeDays = 3,
            IsActive = true,
            Address = "123 Cancel St",
            Email = "cancel@supplier.test",
            Phone = "+911234567890"
        };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), Name = "Cancel WH", Code = "WH-CGRN" };
        var role = await _context.Roles.FirstAsync(r => r.Name == "Staff");
        var creator = new User
        {
            Id = Guid.NewGuid(),
            FullName = "PO Creator",
            Email = "creator@cancel.test",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = role.Id,
            Role = role
        };
        var receiver = new User
        {
            Id = Guid.NewGuid(),
            FullName = "GRN Receiver",
            Email = "receiver@cancel.test",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = role.Id,
            Role = role
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Cancel Widget",
            SKU = "CGRN-001",
            CostPrice = 10m,
            SellingPrice = 15m,
            ReorderPoint = 5,
            SafetyStockQty = 2,
            IsActive = true,
            CategoryId = category.Id,
            Category = category,
            Length = 1m,
            Width = 1m,
            Height = 1m,
            WeightKg = 1m,
            PreferredBinType = BinType.Standard
        };
        var zone = new WarehouseZone
        {
            Id = Guid.NewGuid(),
            Name = "Storage Zone",
            Code = "ZONE-CGRN",
            ZoneType = ZoneType.Storage,
            WarehouseId = warehouse.Id,
            AreaSqFt = 100,
            MaxVolumeCm3 = 10000,
            MaxWeightKg = 10000
        };
        var bin = new BinLocation
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-CGRN",
            Barcode = "BIN-CGRN-BC",
            ZoneId = zone.Id,
            Zone = zone,
            BinType = BinType.Standard,
            MaxVolumeCm3 = 1000m,
            MaxWeightKg = 1000m
        };
        var attachment = new FileAttachment
        {
            Id = Guid.NewGuid(),
            EntityType = "PurchaseOrder",
            EntityId = Guid.NewGuid(),
            FileName = "challan.pdf",
            FilePath = "/tmp/challan.pdf",
            MimeType = "application/pdf",
            FileSizeBytes = 1,
            Category = DocumentCategory.DeliveryChallan,
            UploadedBy = creator.Id
        };
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = "PO-TEST-011",
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            CreatedBy = creator.Id,
            Status = PurchaseOrderStatus.Approved,
            TotalAmount = 30m,
            CreatedAt = DateTime.UtcNow,
            Supplier = supplier,
            Warehouse = warehouse,
            CreatedByUser = creator,
            Items = []
        };
        var poItem = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = po.Id,
            ProductId = product.Id,
            Product = product,
            QuantityOrdered = 3,
            QuantityReceived = 0,
            UnitPrice = 10m,
            TotalPrice = 30m
        };
        po.Items.Add(poItem);

        await _context.Categories.AddAsync(category);
        await _context.Suppliers.AddAsync(supplier);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.Users.AddAsync(creator);
        await _context.Users.AddAsync(receiver);
        await _context.Products.AddAsync(product);
        await _context.WarehouseZones.AddAsync(zone);
        await _context.BinLocations.AddAsync(bin);
        await _context.FileAttachments.AddAsync(attachment);
        await _context.PurchaseOrders.AddAsync(po);
        await _context.SaveChangesAsync();

        var received = await _service.ReceiveGoodsAsync(new GoodsReceiptCreateDto
        {
            PurchaseOrderId = po.Id,
            ReceivedBy = receiver.Id,
            WarehouseId = warehouse.Id,
            AttachmentIds = [attachment.Id],
            Items =
            [
                new GoodsReceiptItemDto
                {
                    PurchaseOrderItemId = poItem.Id,
                    BinLocationId = bin.Id,
                    QuantityReceived = 3,
                    QuantityRejected = 0
                }
            ]
        });

        var stockAfterReceipt = await _context.StockLevels.SingleAsync(sl =>
            sl.ProductId == product.Id &&
            sl.WarehouseId == warehouse.Id &&
            sl.BinLocationId == bin.Id);
        Assert.Equal(3, stockAfterReceipt.QuantityOnHand);
        Assert.Equal(PurchaseOrderStatus.Received, (await _context.PurchaseOrders.SingleAsync(p => p.Id == po.Id)).Status);

        // Act
        var cancelled = await _service.CancelGoodsReceiptAsync(received.Id, creator.Id);

        // Assert
        Assert.True(cancelled);

        var revertedPo = await _context.PurchaseOrders.SingleAsync(p => p.Id == po.Id);
        Assert.Equal(PurchaseOrderStatus.Approved, revertedPo.Status);

        var revertedReceipt = await _context.GoodsReceipts.SingleAsync(g => g.Id == received.Id);
        Assert.Equal(GoodsReceiptStatus.Cancelled, revertedReceipt.Status);

        var revertedStock = await _context.StockLevels.SingleAsync(sl =>
            sl.ProductId == product.Id &&
            sl.WarehouseId == warehouse.Id &&
            sl.BinLocationId == bin.Id);
        Assert.Equal(0, revertedStock.QuantityOnHand);
    }

    [Fact]
    public async Task ReceiveGoodsAsync_PartialAcceptance_PublishesVarianceAlert()
    {
        // Arrange
        var category = new Category { Id = Guid.NewGuid(), Name = "Variance", Description = "Variance category" };
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = "Variance Supplier",
            Code = "SUP-VAR",
            LeadTimeDays = 4,
            IsActive = true,
            Address = "123 Variance St",
            Email = "variance@supplier.test",
            Phone = "+911234567890"
        };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), Name = "Variance WH", Code = "WH-VAR" };
        var role = await _context.Roles.FirstAsync(r => r.Name == "Staff");
        var creator = new User
        {
            Id = Guid.NewGuid(),
            FullName = "PO Creator",
            Email = "creator@variance.test",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = role.Id,
            Role = role
        };
        var receiver = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Variance Receiver",
            Email = "receiver@variance.test",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = role.Id,
            Role = role
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Variance Widget",
            SKU = "VAR-001",
            CostPrice = 10m,
            SellingPrice = 15m,
            ReorderPoint = 5,
            SafetyStockQty = 2,
            IsActive = true,
            CategoryId = category.Id,
            Category = category,
            Length = 1m,
            Width = 1m,
            Height = 1m,
            WeightKg = 1m,
            PreferredBinType = BinType.Standard
        };
        var zone = new WarehouseZone
        {
            Id = Guid.NewGuid(),
            Name = "Storage Zone",
            Code = "ZONE-VAR",
            ZoneType = ZoneType.Storage,
            WarehouseId = warehouse.Id,
            AreaSqFt = 100,
            MaxVolumeCm3 = 10000,
            MaxWeightKg = 10000
        };
        var bin = new BinLocation
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-VAR",
            Barcode = "BIN-VAR-BC",
            ZoneId = zone.Id,
            Zone = zone,
            BinType = BinType.Standard,
            MaxVolumeCm3 = 1000m,
            MaxWeightKg = 1000m
        };
        var attachment = new FileAttachment
        {
            Id = Guid.NewGuid(),
            EntityType = "PurchaseOrder",
            EntityId = Guid.NewGuid(),
            FileName = "challan.pdf",
            FilePath = "/tmp/challan.pdf",
            MimeType = "application/pdf",
            FileSizeBytes = 1,
            Category = DocumentCategory.DeliveryChallan,
            UploadedBy = creator.Id
        };
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = "PO-TEST-012",
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            CreatedBy = creator.Id,
            Status = PurchaseOrderStatus.Approved,
            TotalAmount = 20m,
            CreatedAt = DateTime.UtcNow,
            Supplier = supplier,
            Warehouse = warehouse,
            CreatedByUser = creator,
            Items = []
        };
        var poItem = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = po.Id,
            ProductId = product.Id,
            Product = product,
            QuantityOrdered = 2,
            QuantityReceived = 0,
            UnitPrice = 10m,
            TotalPrice = 20m
        };
        po.Items.Add(poItem);

        await _context.Categories.AddAsync(category);
        await _context.Suppliers.AddAsync(supplier);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.Users.AddAsync(creator);
        await _context.Users.AddAsync(receiver);
        await _context.Products.AddAsync(product);
        await _context.WarehouseZones.AddAsync(zone);
        await _context.BinLocations.AddAsync(bin);
        await _context.FileAttachments.AddAsync(attachment);
        await _context.PurchaseOrders.AddAsync(po);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ReceiveGoodsAsync(new GoodsReceiptCreateDto
        {
            PurchaseOrderId = po.Id,
            ReceivedBy = receiver.Id,
            WarehouseId = warehouse.Id,
            Notes = "Partial variance",
            AttachmentIds = [attachment.Id],
            Items =
            [
                new GoodsReceiptItemDto
                {
                    PurchaseOrderItemId = poItem.Id,
                    BinLocationId = bin.Id,
                    QuantityReceived = 2,
                    QuantityRejected = 1,
                    RejectionReason = "Damaged carton"
                }
            ]
        });

        // Assert
        Assert.Equal(GoodsReceiptStatus.PartiallyAccepted, result.Status);
        _notificationMock.Verify(n => n.SendGoodsReceiptVarianceAlertAsync(
            po.Id,
            It.IsAny<Guid>(),
            1,
            1,
            It.Is<string?>(s => s == "Damaged carton"),
            It.IsAny<decimal>()), Times.Once);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
