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

public class InvoiceProcessingServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _uow;
    private readonly Mock<INotificationService> _notificationMock;
    private readonly InvoiceProcessingService _service;

    public InvoiceProcessingServiceTests()
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

        _notificationMock = new Mock<INotificationService>();
        _notificationMock.Setup(n => n.SendInvoiceApprovedAlertAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        _notificationMock.Setup(n => n.SendInvoiceRejectedAlertAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _notificationMock.Setup(n => n.SendInvoicePaymentCompletedAlertAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        _notificationMock.Setup(n => n.SendInvoicePaymentFailedAlertAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _service = new InvoiceProcessingService(_uow, _notificationMock.Object);
    }

    // Prevents finance review notes from being lost when moving a pending invoice into review
    [Fact]
    public async Task MarkUnderReviewAsync_PendingInvoice_WithExistingNotes_MovesToUnderReview()
    {
        // Arrange
        var invoice = await SeedInvoiceAsync(
            invoiceAmount: 100m,
            status: SupplierInvoiceStatus.Pending,
            approvedAmount: null,
            internalNotes: "Existing note");

        var dto = new InvoiceActionDto
        {
            InternalNotes = "Needs finance review"
        };

        // Act
        await _service.MarkUnderReviewAsync(invoice.Id, dto);

        // Assert
        var saved = await _context.Set<SupplierInvoice>().FirstAsync(i => i.Id == invoice.Id);
        Assert.Equal(SupplierInvoiceStatus.UnderReview, saved.Status);
        Assert.Equal("Existing note\nNeeds finance review", saved.InternalNotes);
    }

    // Prevents invalid state transitions from skipping the invoice review gate
    [Fact]
    public async Task MarkUnderReviewAsync_NonPendingInvoice_ThrowsBusinessRuleException()
    {
        // Arrange
        var invoice = await SeedInvoiceAsync(
            invoiceAmount: 100m,
            status: SupplierInvoiceStatus.Matched,
            approvedAmount: 100m);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.MarkUnderReviewAsync(invoice.Id, new InvoiceActionDto { InternalNotes = "Review" }));
    }

    // Prevents overpayment by ensuring a valid invoice is matched only when GRN value supports it
    [Fact]
    public async Task MatchInvoiceAsync_InvoiceWithinAcceptedGrnLimit_MatchesAndApprovesAmount()
    {
        // Arrange
        var invoice = await SeedInvoiceAsync(
            invoiceAmount: 100m,
            status: SupplierInvoiceStatus.Pending,
            approvedAmount: null,
            internalNotes: "Needs PO match");

        var dto = new InvoiceActionDto
        {
            InternalNotes = "Finance approved"
        };

        // Act
        var result = await _service.MatchInvoiceAsync(invoice.Id, dto);

        // Assert
        Assert.True(result.IsMatch);
        Assert.Equal(invoice.Id, result.InvoiceId);
        Assert.Equal(100m, result.ApprovedAmount);

        var saved = await _context.Set<SupplierInvoice>().FirstAsync(i => i.Id == invoice.Id);
        Assert.Equal(SupplierInvoiceStatus.Matched, saved.Status);
        Assert.Equal(100m, saved.ApprovedAmount);
        Assert.Equal("Needs PO match\nFinance approved", saved.InternalNotes);
        _notificationMock.Verify(n => n.SendInvoiceApprovedAlertAsync(invoice.Id), Times.Once);
        _notificationMock.Verify(n => n.SendInvoiceRejectedAlertAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    // Prevents invoice amounts that exceed accepted goods receipts from being approved
    [Fact]
    public async Task MatchInvoiceAsync_InvoiceExceedsAcceptedGrnOrPoTotal_RejectedWithReason()
    {
        // Arrange
        var invoice = await SeedInvoiceAsync(
            invoiceAmount: 120m,
            status: SupplierInvoiceStatus.Pending,
            approvedAmount: null,
            internalNotes: "Needs finance review");

        var dto = new InvoiceActionDto
        {
            InternalNotes = "Over billed"
        };

        // Act
        var result = await _service.MatchInvoiceAsync(invoice.Id, dto);

        // Assert
        Assert.False(result.IsMatch);
        Assert.NotEmpty(result.Discrepancies);

        var saved = await _context.Set<SupplierInvoice>().FirstAsync(i => i.Id == invoice.Id);
        Assert.Equal(SupplierInvoiceStatus.Rejected, saved.Status);
        Assert.NotNull(saved.RejectionReason);
        Assert.Contains("exceed", saved.RejectionReason!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Needs finance review\n", saved.InternalNotes!.Substring(0, "Needs finance review\n".Length));
        Assert.Contains("Over billed", saved.InternalNotes);
        _notificationMock.Verify(n => n.SendInvoiceRejectedAlertAsync(invoice.Id, It.IsAny<string>()), Times.Once);
        _notificationMock.Verify(n => n.SendInvoiceApprovedAlertAsync(It.IsAny<Guid>()), Times.Never);
    }

    // Prevents payment posting from running before an invoice is fully matched
    [Fact]
    public async Task PayInvoiceAsync_MatchedInvoice_PaysAndRecordsPaymentReference()
    {
        // Arrange
        var invoice = await SeedInvoiceAsync(
            invoiceAmount: 100m,
            status: SupplierInvoiceStatus.Matched,
            approvedAmount: 100m,
            internalNotes: "Ready for settlement");

        var dto = new InvoicePayDto
        {
            PaymentReference = "TT-12345",
            InternalNotes = "Payment released"
        };

        // Act
        await _service.PayInvoiceAsync(invoice.Id, dto);

        // Assert
        var saved = await _context.Set<SupplierInvoice>().FirstAsync(i => i.Id == invoice.Id);
        Assert.Equal(SupplierInvoiceStatus.Paid, saved.Status);
        Assert.Equal(100m, saved.PaidAmount);
        Assert.Equal("TT-12345", saved.PaymentReference);
        Assert.NotNull(saved.PaidAt);
        Assert.Equal("Ready for settlement\nPayment released", saved.InternalNotes);
        _notificationMock.Verify(n => n.SendInvoicePaymentCompletedAlertAsync(invoice.Id), Times.Once);
    }

    // Prevents payment attempts on invoices that have not passed invoice matching
    [Fact]
    public async Task PayInvoiceAsync_NonMatchedInvoice_ThrowsAndSendsFailureAlert()
    {
        // Arrange
        var invoice = await SeedInvoiceAsync(
            invoiceAmount: 100m,
            status: SupplierInvoiceStatus.Pending,
            approvedAmount: null);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.PayInvoiceAsync(invoice.Id, new InvoicePayDto { PaymentReference = "TT-99999" }));

        _notificationMock.Verify(n => n.SendInvoicePaymentFailedAlertAsync(
            invoice.Id,
            "Only Matched invoices can be paid."), Times.Once);
    }

    // Prevents voiding a paid invoice while still allowing operational cancellation of open invoices
    [Fact]
    public async Task VoidInvoiceAsync_PendingInvoice_VoidsAndStoresRejectionReason()
    {
        // Arrange
        var invoice = await SeedInvoiceAsync(
            invoiceAmount: 100m,
            status: SupplierInvoiceStatus.Pending,
            approvedAmount: null,
            internalNotes: "Pending payment");

        var dto = new InvoiceRejectDto
        {
            RejectionReason = "Duplicate upload",
            InternalNotes = "Finance voided the invoice"
        };

        // Act
        await _service.VoidInvoiceAsync(invoice.Id, dto);

        // Assert
        var saved = await _context.Set<SupplierInvoice>().FirstAsync(i => i.Id == invoice.Id);
        Assert.Equal(SupplierInvoiceStatus.Voided, saved.Status);
        Assert.Equal("Duplicate upload", saved.RejectionReason);
        Assert.Equal("Pending payment\nFinance voided the invoice", saved.InternalNotes);
    }

    // Prevents already paid invoices from being voided after cash has left the business
    [Fact]
    public async Task VoidInvoiceAsync_PaidInvoice_ThrowsBusinessRuleException()
    {
        // Arrange
        var invoice = await SeedInvoiceAsync(
            invoiceAmount: 100m,
            status: SupplierInvoiceStatus.Paid,
            approvedAmount: 100m,
            internalNotes: "Already settled");

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.VoidInvoiceAsync(invoice.Id, new InvoiceRejectDto { RejectionReason = "Late cancellation" }));
    }

    private async Task<SupplierInvoice> SeedInvoiceAsync(
        decimal invoiceAmount,
        SupplierInvoiceStatus status,
        decimal? approvedAmount,
        string? internalNotes = null)
    {
        var staffRole = await _context.Roles.FirstAsync(r => r.Name == "Staff");

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = "Acme Supplies",
            Code = "SUP-100",
            Email = "supplier@acme.test",
            Phone = "+911234567890",
            Address = "123 Supplier Lane",
            LeadTimeDays = 7,
            IsActive = true
        };

        var supplierContact = new SupplierContact
        {
            Id = Guid.NewGuid(),
            SupplierId = supplier.Id,
            FullName = "Supplier Contact",
            Email = "contact@acme.test",
            Phone = "+919876543210",
            PasswordHash = "hash",
            IsActive = true
        };

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Name = "Main Warehouse",
            Code = "WH-100"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Receiving User",
            Email = "receiver@acme.test",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = staffRole.Id,
            Role = staffRole
        };

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "General",
            Description = "General stock"
        };

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Widget",
            SKU = "WGT-001",
            CostPrice = 10m,
            SellingPrice = 15m,
            IsActive = true,
            ReorderPoint = 5,
            CategoryId = category.Id,
            Category = category
        };

        var poItem = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Product = product,
            QuantityOrdered = 10,
            QuantityReceived = 0,
            UnitPrice = 10m,
            TotalPrice = 100m
        };

        var purchaseOrder = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = "PO-1001",
            SupplierId = supplier.Id,
            Supplier = supplier,
            WarehouseId = warehouse.Id,
            Warehouse = warehouse,
            CreatedBy = user.Id,
            CreatedByUser = user,
            Status = PurchaseOrderStatus.Approved,
            TotalAmount = 100m,
            CreatedAt = DateTime.UtcNow,
            Items = [poItem],
            GoodsReceipts = [],
            SupplierInvoices = []
        };

        poItem.PurchaseOrderId = purchaseOrder.Id;
        poItem.PurchaseOrder = purchaseOrder;

        var goodsReceipt = new GoodsReceipt
        {
            Id = Guid.NewGuid(),
            GrnNumber = "GRN-1001",
            PurchaseOrderId = purchaseOrder.Id,
            PurchaseOrder = purchaseOrder,
            WarehouseId = warehouse.Id,
            Warehouse = warehouse,
            ReceivedBy = user.Id,
            ReceivedByUser = user,
            Status = GoodsReceiptStatus.Accepted,
            ReceivedDate = DateTime.UtcNow,
            Items = []
        };

        var goodsReceiptItem = new GoodsReceiptItem
        {
            Id = Guid.NewGuid(),
            GoodsReceiptId = goodsReceipt.Id,
            GoodsReceipt = goodsReceipt,
            PurchaseOrderItemId = poItem.Id,
            PurchaseOrderItem = poItem,
            QuantityReceived = 10,
            QuantityRejected = 0
        };

        goodsReceipt.Items.Add(goodsReceiptItem);
        purchaseOrder.GoodsReceipts.Add(goodsReceipt);

        var invoice = new SupplierInvoice
        {
            Id = Guid.NewGuid(),
            SupplierId = supplier.Id,
            Supplier = supplier,
            PurchaseOrderId = purchaseOrder.Id,
            PurchaseOrder = purchaseOrder,
            UploadedByContactId = supplierContact.Id,
            UploadedByContact = supplierContact,
            InvoiceNumber = "INV-1001",
            Amount = invoiceAmount,
            Currency = "INR",
            InvoiceDate = DateTime.UtcNow.Date,
            FilePath = "/tmp/invoice.pdf",
            OriginalFileName = "invoice.pdf",
            Status = status,
            InternalNotes = internalNotes,
            ApprovedAmount = approvedAmount,
            CreatedAt = DateTime.UtcNow
        };

        supplier.PurchaseOrders.Add(purchaseOrder);
        supplier.Contacts.Add(supplierContact);
        supplier.Invoices.Add(invoice);
        purchaseOrder.SupplierInvoices.Add(invoice);

        await _context.Categories.AddAsync(category);
        await _context.Suppliers.AddAsync(supplier);
        await _context.SupplierContacts.AddAsync(supplierContact);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.Users.AddAsync(user);
        await _context.Products.AddAsync(product);
        await _context.PurchaseOrders.AddAsync(purchaseOrder);
        await _context.GoodsReceipts.AddAsync(goodsReceipt);
        await _context.SupplierInvoices.AddAsync(invoice);
        await _context.SaveChangesAsync();

        return invoice;
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
