using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;
using SmartInventory.Repository;
using SmartInventory.Repository.Repositories;
using SmartInventory.Repository.Services;
using SmartInventory.Service.Services;
using Xunit.Abstractions;
using MediatR;

namespace SmartInventory.Tests;

public class EfInsertSqlProbeTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _uow;
    private readonly ProductService _productService;
    private readonly WarehouseService _warehouseService;
    private readonly SupplierInvoiceService _supplierInvoiceService;
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IBarcodeService> _barcodeMock = new();
    private readonly Mock<INotificationService> _notificationMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly ISequenceNumberGenerator _sequenceNumberGenerator;
    private readonly ITestOutputHelper _output;
    private readonly List<string> _sqlLogs = [];

    public EfInsertSqlProbeTests(ITestOutputHelper output)
    {
        _output = output;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=smart_inventory;Username=postgres;Password=12345")
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .LogTo(message => _sqlLogs.Add(message), LogLevel.Information)
            .Options;

        _context = new AppDbContext(options);

        var products = new ProductRepository(_context);
        var suppliers = new SupplierRepository(_context);
        var purchaseOrders = new PurchaseOrderRepository(_context);
        var transfers = new TransferRepository(_context);
        var barcodes = new BarcodeRepository(_context);
        var notifications = new NotificationRepository(_context);
        var stockLevels = new StockLevelRepository(_context);

        _uow = new UnitOfWork(_context, products, suppliers, purchaseOrders, transfers, barcodes, notifications, stockLevels);
        _sequenceNumberGenerator = new SequenceNumberGenerator(_context);

        _cacheMock.Setup(x => x.GetAsync<ProductResponseDto>(It.IsAny<string>())).ReturnsAsync((ProductResponseDto?)null);
        _cacheMock.Setup(x => x.GetAsync<WarehouseResponseDto>(It.IsAny<string>())).ReturnsAsync((WarehouseResponseDto?)null);
        _cacheMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);
        _cacheMock.Setup(x => x.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        _notificationMock.Setup(x => x.SendInvoiceUploadedAlertAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);

        _fileStorageMock
            .Setup(x => x.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync((Stream _, string _, string _, string? prefix) => $"uploads/{prefix ?? "probe"}/invoice.pdf");

        _productService = new ProductService(_uow, Mock.Of<ICurrentUserService>(), _barcodeMock.Object, _cacheMock.Object, _sequenceNumberGenerator);
        _warehouseService = new WarehouseService(_uow, _cacheMock.Object, _notificationMock.Object, _sequenceNumberGenerator);
        _supplierInvoiceService = new SupplierInvoiceService(_uow, _fileStorageMock.Object, _notificationMock.Object, _sequenceNumberGenerator);
    }

    [Fact]
    public async Task Print_InsertSql_And_SequenceConsumption_For_Key_NumberedEntities()
    {
        var seed = await SeedInvoicePrerequisitesAsync();

        var productBefore = await ReadSequenceStateAsync("seq_products");
        var warehouseBefore = await ReadSequenceStateAsync("seq_warehouses");
        var zoneBefore = await ReadSequenceStateAsync("seq_zones");
        var binBefore = await ReadSequenceStateAsync("seq_bins");
        var invoiceBefore = await ReadSequenceStateAsync("seq_invoices");

        var productLogStart = _sqlLogs.Count;
        var product = await _productService.CreateProductAsync(new ProductCreateDto
        {
            Name = "Probe Product",
            Description = "probe",
            UnitOfMeasure = UnitOfMeasure.Piece,
            CostPrice = 1,
            SellingPrice = 2,
            ReorderPoint = 1,
            ReorderQuantity = 1,
            CategoryId = seed.Category.Id,
            IsActive = true
        });
        PrintSection("PRODUCT", _sqlLogs[productLogStart..]);

        var warehouseLogStart = _sqlLogs.Count;
        var warehouse = await _warehouseService.CreateWarehouseAsync(new WarehouseCreateDto
        {
            Name = "Probe Warehouse",
            State = "KA",
            IsActive = true,
            AreaSqFt = 100,
            MaxVolumeCm3 = 1000,
            MaxWeightKg = 1000
        });
        PrintSection("WAREHOUSE", _sqlLogs[warehouseLogStart..]);

        var zoneLogStart = _sqlLogs.Count;
        var zone = await _warehouseService.CreateZoneAsync(new ZoneCreateDto
        {
            WarehouseId = warehouse.Id,
            Name = "Probe Zone",
            ZoneType = ZoneType.Storage,
            IsActive = true,
            AreaSqFt = 20,
            MaxVolumeCm3 = 200,
            MaxWeightKg = 200
        });
        PrintSection("ZONE", _sqlLogs[zoneLogStart..]);

        var binLogStart = _sqlLogs.Count;
        var bin = await _warehouseService.CreateBinLocationAsync(new BinLocationCreateDto
        {
            ZoneId = zone.Id,
            BinType = BinType.Standard,
            MaxVolumeCm3 = 50,
            MaxWeightKg = 50,
            IsActive = true
        });
        PrintSection("BIN", _sqlLogs[binLogStart..]);

        var invoiceLogStart = _sqlLogs.Count;
        var invoice = await _supplierInvoiceService.UploadInvoiceAsync(
            seed.Supplier.Id,
            seed.Contact.Id,
            new SupplierUploadInvoiceRequest(
                seed.PurchaseOrder.Id,
                100m,
                "INR",
                DateTime.UtcNow.Date,
                new MemoryStream(new byte[] { 1, 2, 3 }),
                "probe-invoice.pdf",
                "application/pdf"));
        PrintSection("SUPPLIER_INVOICE", _sqlLogs[invoiceLogStart..]);

        var productAfter = await ReadSequenceStateAsync("seq_products");
        var warehouseAfter = await ReadSequenceStateAsync("seq_warehouses");
        var zoneAfter = await ReadSequenceStateAsync("seq_zones");
        var binAfter = await ReadSequenceStateAsync("seq_bins");
        var invoiceAfter = await ReadSequenceStateAsync("seq_invoices");

        PrintSequenceDelta("seq_products", productBefore, productAfter, 1);
        PrintSequenceDelta("seq_warehouses", warehouseBefore, warehouseAfter, 1);
        PrintSequenceDelta("seq_zones", zoneBefore, zoneAfter, 1);
        PrintSequenceDelta("seq_bins", binBefore, binAfter, 1);
        PrintSequenceDelta("seq_invoices", invoiceBefore, invoiceAfter, 1);

        Assert.False(string.IsNullOrWhiteSpace(product.SKU));
        Assert.False(string.IsNullOrWhiteSpace(warehouse.Code));
        Assert.False(string.IsNullOrWhiteSpace(zone.Code));
        Assert.False(string.IsNullOrWhiteSpace(bin.BinCode));
        Assert.False(string.IsNullOrWhiteSpace(invoice.InvoiceNumber));
    }

    private void PrintSection(string label, IEnumerable<string> messages)
    {
        var sql = messages
            .Where(m => m.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase) ||
                        m.Contains("UPDATE", StringComparison.OrdinalIgnoreCase))
            .ToList();

        _output.WriteLine($"[{label}] SQL");
        Console.WriteLine($"[{label}] SQL");

        foreach (var line in sql)
        {
            _output.WriteLine(line);
            Console.WriteLine(line);
        }
    }

    private async Task<(long consumed, bool isCalled)> ReadSequenceStateAsync(string sequenceName)
    {
        await using var conn = _context.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT last_value, is_called FROM {sequenceName}";

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException($"Sequence {sequenceName} did not return a row.");

        var lastValue = reader.GetInt64(0);
        var isCalled = reader.GetBoolean(1);

        return (isCalled ? lastValue : 0L, isCalled);
    }

    private void PrintSequenceDelta(string sequenceName, (long consumed, bool isCalled) before, (long consumed, bool isCalled) after, int expectedDelta)
    {
        var delta = after.consumed - before.consumed;
        var message = $"{sequenceName}: before={before.consumed} after={after.consumed} delta={delta} expected={expectedDelta}";
        _output.WriteLine(message);
        Console.WriteLine(message);
        Assert.Equal(expectedDelta, delta);
    }

    private async Task<(Category Category, Supplier Supplier, SupplierContact Contact, PurchaseOrder PurchaseOrder)> SeedInvoicePrerequisitesAsync()
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
            Phone = "+919999999999"
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
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var supplierProduct = new SupplierProduct
        {
            Id = Guid.NewGuid(),
            SupplierId = supplier.Id,
            ProductId = product.Id,
            UnitPrice = 10m,
            LeadTimeDays = 1,
            MinOrderQuantity = 1,
            IsPreferred = true,
            CreatedAt = DateTime.UtcNow
        };

        var purchaseOrder = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = "PO-PROBE-1",
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            CreatedBy = user.Id,
            Status = PurchaseOrderStatus.Draft,
            TotalAmount = 100m,
            CreatedAt = DateTime.UtcNow,
            Supplier = supplier,
            Warehouse = warehouse,
            CreatedByUser = user,
            Items = []
        };

        var poItem = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = purchaseOrder.Id,
            ProductId = product.Id,
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
            ReceivedBy = user.Id,
            WarehouseId = warehouse.Id,
            PurchaseOrder = purchaseOrder,
            ReceivedByUser = user,
            Warehouse = warehouse,
            Items = []
        };

        var receiptItem = new GoodsReceiptItem
        {
            Id = Guid.NewGuid(),
            GoodsReceiptId = receipt.Id,
            PurchaseOrderItemId = poItem.Id,
            QuantityReceived = 10,
            QuantityRejected = 0,
            QualityCheckStatus = QualityCheckStatus.Passed,
            GoodsReceipt = receipt,
            PurchaseOrderItem = poItem
        };

        receipt.Items.Add(receiptItem);

        await _context.AddRangeAsync(category, supplier, user, contact, warehouse, product, supplierProduct, purchaseOrder, poItem, receipt, receiptItem);
        await _context.SaveChangesAsync();

        return (category, supplier, contact, purchaseOrder);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
