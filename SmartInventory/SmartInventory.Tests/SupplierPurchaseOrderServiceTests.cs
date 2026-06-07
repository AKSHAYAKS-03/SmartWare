using Microsoft.EntityFrameworkCore;
using Moq;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;
using SmartInventory.Repository;
using SmartInventory.Repository.Repositories;
using SmartInventory.Service.Services;
using Xunit;

namespace SmartInventory.Tests;

public class SupplierPurchaseOrderServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ISupplierPurchaseOrderService _service;
    private readonly Guid _supplierId;
    private readonly Guid _poId;
    private readonly Guid _poItemId;

    public SupplierPurchaseOrderServiceTests()
    {
        _context = TestDbContextFactory.Create();

        var products = new Mock<IProductRepository>();
        var suppliers = new Mock<ISupplierRepository>();
        var purchaseOrders = new Mock<IPurchaseOrderRepository>();
        var transfers = new Mock<ITransferRepository>();
        var barcodes = new Mock<IBarcodeRepository>();
        var notifications = new Mock<INotificationRepository>();
        var stockLevels = new Mock<IStockLevelRepository>();

        var uow = new UnitOfWork(_context, products.Object, suppliers.Object,
            purchaseOrders.Object, transfers.Object, barcodes.Object,
            notifications.Object, stockLevels.Object);

        var notificationService = new Mock<INotificationService>();
        notificationService.Setup(n => n.SendNotificationAsync(
                It.IsAny<Guid>(), It.IsAny<NotificationChannel>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);

        _service = new SupplierPurchaseOrderService(uow, notificationService.Object);

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = "Supplier One",
            Code = "SUP-001",
            LeadTimeDays = 5,
            IsActive = true,
            Address = "123 Supplier St",
            Email = "supplier@example.com",
            Phone = "+911234567890"
        };

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Name = "Main Warehouse",
            Code = "WH-001"
        };

        var role = _context.Roles.First(r => r.Name == "Staff");
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "PO Creator",
            Email = "creator@example.com",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = role.Id,
            Role = role
        };

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Category",
            Description = "Category"
        };

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Widget",
            SKU = "WGT-001",
            CostPrice = 10m,
            SellingPrice = 15m,
            ReorderPoint = 5,
            IsActive = true,
            CategoryId = category.Id,
            Category = category
        };

        _supplierId = supplier.Id;
        _poId = Guid.NewGuid();
        _poItemId = Guid.NewGuid();

        var poItem = new PurchaseOrderItem
        {
            Id = _poItemId,
            PurchaseOrderId = _poId,
            ProductId = product.Id,
            Product = product,
            QuantityOrdered = 10,
            QuantityReceived = 0,
            UnitPrice = 10m,
            TotalPrice = 100m
        };

        var purchaseOrder = new PurchaseOrder
        {
            Id = _poId,
            PoNumber = "PO-TEST-001",
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            CreatedBy = user.Id,
            Status = PurchaseOrderStatus.Approved,
            TotalAmount = 100m,
            CreatedAt = DateTime.UtcNow,
            Supplier = supplier,
            Warehouse = warehouse,
            CreatedByUser = user,
            Items = [poItem]
        };

        _context.Categories.Add(category);
        _context.Suppliers.Add(supplier);
        _context.Warehouses.Add(warehouse);
        _context.Users.Add(user);
        _context.Products.Add(product);
        _context.PurchaseOrders.Add(purchaseOrder);
        _context.SaveChanges();
    }

    [Fact]
    public async Task CreateShipmentAsync_Returns_FinalShipmentNumber()
    {
        var result = await _service.CreateShipmentAsync(
            _supplierId,
            _poId,
            new SupplierCreateShipmentRequest(
                TrackingNumber: "TRACK-001",
                CarrierName: "DHL",
                ExpectedDelivery: DateTime.UtcNow.AddDays(2),
                SupplierNotes: "Leave at dock",
                Lines: null));

        Assert.NotEqual("TEMP", result.ShipmentNumber);
        Assert.StartsWith("SHP-", result.ShipmentNumber);
        Assert.Single(result.Items);
        Assert.Equal(_poItemId, result.Items[0].PurchaseOrderItemId);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
