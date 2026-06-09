using Microsoft.EntityFrameworkCore;
using Moq;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;
using SmartInventory.Repository;
using SmartInventory.Repository.Repositories;
using SmartInventory.Service.Services;
using Xunit;

namespace SmartInventory.Tests;

public class SupplierInvoiceServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _uow;
    private readonly Mock<IFileStorageService> _fileStorageMock;
    private readonly Mock<INotificationService> _notificationMock;
    private readonly Mock<ISequenceNumberGenerator> _sequenceMock;
    private readonly SupplierInvoiceService _service;

    public SupplierInvoiceServiceTests()
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

        _fileStorageMock = new Mock<IFileStorageService>();
        _fileStorageMock
            .Setup(x => x.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync((Stream _, string _, string _, string? prefix) => $"uploads/{prefix ?? "invoice"}/invoice.pdf");

        _notificationMock = new Mock<INotificationService>();
        _notificationMock.Setup(x => x.SendInvoiceUploadedAlertAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _sequenceMock = new Mock<ISequenceNumberGenerator>();
        _sequenceMock.Setup(x => x.GenerateAsync("seq_invoices", "INV"))
            .ReturnsAsync("INV-0001");

        _service = new SupplierInvoiceService(
            _uow,
            _fileStorageMock.Object,
            _notificationMock.Object,
            _sequenceMock.Object);
    }

    // Prevents invoice uploads from bypassing GRN-backed billing ceilings
    [Fact]
    public async Task UploadInvoiceAsync_WithinAcceptedGrnCeiling_CreatesPendingInvoice()
    {
        // Arrange
        var seed = await SeedPurchaseOrderGraphAsync();
        var request = new SupplierUploadInvoiceRequest(
            seed.PurchaseOrder.Id,
            100m,
            "INR",
            DateTime.UtcNow.Date,
            new MemoryStream(new byte[] { 1, 2, 3 }),
            "invoice.pdf",
            "application/pdf");

        // Act
        var result = await _service.UploadInvoiceAsync(seed.Supplier.Id, seed.Contact.Id, request);

        // Assert
        Assert.Equal("INV-0001", result.InvoiceNumber);
        Assert.Equal(seed.PurchaseOrder.PoNumber, result.PoNumber);
        Assert.Equal(100m, result.Amount);
        Assert.Equal(SupplierInvoiceStatus.Pending, result.Status);
        Assert.Equal("invoice.pdf", result.OriginalFileName);

        var saved = await _context.Set<SupplierInvoice>().SingleAsync(i => i.InvoiceNumber == "INV-0001");
        Assert.Equal(SupplierInvoiceStatus.Pending, saved.Status);
        Assert.Equal(seed.Supplier.Id, saved.SupplierId);
        Assert.Equal(seed.PurchaseOrder.Id, saved.PurchaseOrderId);
        Assert.Equal("uploads/SUPINV_PO-PROBE-1/invoice.pdf", saved.FilePath);

        _fileStorageMock.Verify(x => x.SaveFileAsync(
            It.IsAny<Stream>(),
            "invoice.pdf",
            "supplier-invoices",
            "SUPINV_PO-PROBE-1"), Times.Once);
        _notificationMock.Verify(x => x.SendInvoiceUploadedAlertAsync(saved.Id), Times.Once);
    }

    // Prevents duplicate or over-billed supplier invoices from being accepted
    [Fact]
    public async Task UploadInvoiceAsync_ExceedsAcceptedGrnCeiling_ThrowsBusinessRuleException()
    {
        // Arrange
        var seed = await SeedPurchaseOrderGraphAsync();
        var request = new SupplierUploadInvoiceRequest(
            seed.PurchaseOrder.Id,
            120m,
            "INR",
            DateTime.UtcNow.Date,
            new MemoryStream(new byte[] { 1, 2, 3 }),
            "invoice.pdf",
            "application/pdf");

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.UploadInvoiceAsync(seed.Supplier.Id, seed.Contact.Id, request));

        Assert.False(await _context.Set<SupplierInvoice>().AnyAsync());
        _fileStorageMock.Verify(x => x.SaveFileAsync(
            It.IsAny<Stream>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>()), Times.Never);
        _notificationMock.Verify(x => x.SendInvoiceUploadedAlertAsync(It.IsAny<Guid>()), Times.Never);
    }

    private async Task<(Supplier Supplier, SupplierContact Contact, PurchaseOrder PurchaseOrder)> SeedPurchaseOrderGraphAsync()
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Probe Category",
            Slug = $"probe-category-{Guid.NewGuid():N}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = "Probe Supplier",
            Code = $"SUP-PROBE-{Guid.NewGuid():N}".Substring(0, 20),
            LeadTimeDays = 1,
            PaymentTerms = PaymentTerms.Net30,
            CreditLimit = 0m,
            IsActive = true,
            Status = SupplierStatus.Active,
            RegistrationSource = RegistrationSource.AdminInvited,
            Email = "probe@supplier.test",
            Phone = "+919999999999",
            Address = "1 Supplier Road"
        };

        var role = await _context.Roles.FirstAsync(r => r.Name == "Staff");
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Probe Receiver",
            Email = $"probe-receiver-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = role.Id,
            Role = role
        };

        var contact = new SupplierContact
        {
            Id = Guid.NewGuid(),
            SupplierId = supplier.Id,
            FullName = "Probe Contact",
            Email = "probe.contact@supplier.test",
            PasswordHash = "hash",
            Phone = "+919999999998",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Name = "Probe Invoice Warehouse",
            Code = "WH-PROBE-1",
            State = "KA",
            IsActive = true,
            AreaSqFt = 100,
            MaxVolumeCm3 = 1000,
            MaxWeightKg = 1000,
            CreatedAt = DateTime.UtcNow
        };

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Probe Invoice Product",
            SKU = "PRD-PROBE-1",
            UnitOfMeasure = UnitOfMeasure.Piece,
            CostPrice = 10m,
            SellingPrice = 12m,
            ReorderPoint = 1,
            ReorderQuantity = 1,
            CategoryId = category.Id,
            Category = category,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var purchaseOrder = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = "PO-PROBE-1",
            SupplierId = supplier.Id,
            Supplier = supplier,
            WarehouseId = warehouse.Id,
            Warehouse = warehouse,
            CreatedBy = user.Id,
            CreatedByUser = user,
            Status = PurchaseOrderStatus.Approved,
            TotalAmount = 100m,
            CreatedAt = DateTime.UtcNow,
            Items = [],
            GoodsReceipts = [],
            SupplierInvoices = []
        };

        var poItem = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = purchaseOrder.Id,
            PurchaseOrder = purchaseOrder,
            ProductId = product.Id,
            Product = product,
            QuantityOrdered = 10,
            QuantityReceived = 0,
            UnitPrice = 10m,
            TotalPrice = 100m,
            CreatedAt = DateTime.UtcNow
        };

        var receipt = new GoodsReceipt
        {
            Id = Guid.NewGuid(),
            GrnNumber = "GRN-PROBE-1",
            ReceivedDate = DateTime.UtcNow.Date,
            Status = GoodsReceiptStatus.Accepted,
            PurchaseOrderId = purchaseOrder.Id,
            PurchaseOrder = purchaseOrder,
            ReceivedBy = user.Id,
            ReceivedByUser = user,
            WarehouseId = warehouse.Id,
            Warehouse = warehouse,
            Items = []
        };

        var receiptItem = new GoodsReceiptItem
        {
            Id = Guid.NewGuid(),
            GoodsReceiptId = receipt.Id,
            GoodsReceipt = receipt,
            PurchaseOrderItemId = poItem.Id,
            PurchaseOrderItem = poItem,
            QuantityReceived = 10,
            QuantityRejected = 0,
            QualityCheckStatus = QualityCheckStatus.Passed,
            CreatedAt = DateTime.UtcNow
        };

        purchaseOrder.Items.Add(poItem);
        purchaseOrder.GoodsReceipts.Add(receipt);
        receipt.Items.Add(receiptItem);
        supplier.PurchaseOrders.Add(purchaseOrder);
        supplier.Contacts.Add(contact);

        await _context.Categories.AddAsync(category);
        await _context.Suppliers.AddAsync(supplier);
        await _context.SupplierContacts.AddAsync(contact);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.Users.AddAsync(user);
        await _context.Products.AddAsync(product);
        await _context.PurchaseOrders.AddAsync(purchaseOrder);
        await _context.GoodsReceipts.AddAsync(receipt);
        await _context.SaveChangesAsync();

        return (supplier, contact, purchaseOrder);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
