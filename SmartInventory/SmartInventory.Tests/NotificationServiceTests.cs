using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;
using SmartInventory.Infrastructure.Services;
using SmartInventory.Repository;
using SmartInventory.Repository.Repositories;
using SmartInventory.Service.Services;
using Xunit;

namespace SmartInventory.Tests;

public class NotificationServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UnitOfWork _uow;
    private readonly Mock<IRealtimeService> _realtimeMock;
    private readonly Mock<IEmailService> _emailMock;
    private readonly IMemoryCache _memoryCache;
    private readonly MemoryCacheService _cacheService;
    private readonly NotificationService _service;

    public NotificationServiceTests()
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

        _realtimeMock = new Mock<IRealtimeService>(MockBehavior.Strict);
        _realtimeMock
            .Setup(r => r.SendNotificationToUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);

        _emailMock = new Mock<IEmailService>(MockBehavior.Strict);
        _emailMock
            .Setup(e => e.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>()))
            .Returns(Task.CompletedTask);

        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _cacheService = new MemoryCacheService(_memoryCache);

        _service = new NotificationService(
            _uow,
            _realtimeMock.Object,
            new NullLogger<NotificationService>(),
            _emailMock.Object,
            _cacheService);
    }

    [Fact]
    public async Task LowStockAlert_FansOut_To_Admin_Email_And_Warehouse_InApp_Then_Cooldown_Suppresses_Duplicate()
    {
        var managerRole = await _context.Roles.FirstAsync(r => r.Name == "Manager");
        var staffRole = await _context.Roles.FirstAsync(r => r.Name == "Staff");

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Widget",
            SKU = "WGT-001",
            ReorderPoint = 10,
            SafetyStockQty = 5,
            UnitOfMeasure = UnitOfMeasure.Piece,
            IsActive = true,
            CategoryId = Guid.NewGuid()
        };

        var category = new Category
        {
            Id = product.CategoryId,
            Name = "General",
            Description = "General category",
            IsActive = true
        };

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Name = "Main WH",
            Code = "WH-01",
            ManagerId = Guid.NewGuid(),
            IsActive = true
        };

        var manager = new User
        {
            Id = warehouse.ManagerId.Value,
            FullName = "Warehouse Manager",
            Email = "manager@test.com",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = managerRole.Id,
            Role = managerRole
        };

        var warehouseUser = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Warehouse Staff",
            Email = "staff@test.com",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = staffRole.Id,
            Role = staffRole
        };

        var admin = await _context.Users
            .Include(u => u.Role)
            .FirstAsync(u => u.Role.Name == "Admin");

        var access = new UserWarehouseAccess
        {
            Id = Guid.NewGuid(),
            UserId = warehouseUser.Id,
            WarehouseId = warehouse.Id,
            AccessLevel = AccessLevel.Operator,
            GrantedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Products.AddAsync(product);
        await _context.Categories.AddAsync(category);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.Users.AddRangeAsync(manager, warehouseUser);
        await _context.UserWarehouseAccesses.AddAsync(access);
        await _context.SaveChangesAsync();

        await _service.SendLowStockAlertAsync(product.Id, warehouse.Id, currentStock: 4, reorderPoint: 10);

        var notifications = await _context.Notifications
            .Where(n => n.Type == "LowStock")
            .ToListAsync();

        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, n => n.UserId == manager.Id && n.Channel == NotificationChannel.InApp);
        Assert.Contains(notifications, n => n.UserId == warehouseUser.Id && n.Channel == NotificationChannel.InApp);

        var outboxMessages = await _context.OutboxMessages
            .Where(m => m.EventType == "SendNotification")
            .ToListAsync();

        Assert.Equal(3, outboxMessages.Count);

        var processor = CreateOutboxProcessor();
        await RunOutboxProcessorAsync(processor);

        _realtimeMock.Verify(r => r.SendNotificationToUserAsync(
            manager.Id,
            It.IsAny<string>(),
            It.IsAny<string>(),
            "LowStock",
            product.Id), Times.Once);

        _realtimeMock.Verify(r => r.SendNotificationToUserAsync(
            warehouseUser.Id,
            It.IsAny<string>(),
            It.IsAny<string>(),
            "LowStock",
            product.Id), Times.Once);

        _emailMock.Verify(e => e.SendEmailAsync(
            admin.Email,
            "Low Stock Warning",
            It.IsAny<string>(),
            true), Times.Once);

        var logs = await _context.NotificationLogs.ToListAsync();
        Assert.Equal(3, logs.Count);
        Assert.All(logs, log => Assert.Equal(NotificationStatus.Sent, log.Status));

        await _service.SendLowStockAlertAsync(product.Id, warehouse.Id, currentStock: 3, reorderPoint: 10);

        var afterCooldownMessages = await _context.OutboxMessages
            .Where(m => m.EventType == "SendNotification")
            .ToListAsync();

        Assert.Equal(3, afterCooldownMessages.Count);
    }

    [Fact]
    public async Task SupplierInvoiceRejected_Uses_External_Email_Address()
    {
        await _service.SendSupplierInvoiceRejectedNotificationAsync(
            supplierId: Guid.NewGuid(),
            supplierEmail: "supplier@test.com",
            supplierName: "Acme Supplies",
            invoiceNumber: "INV-1001",
            poNumber: "PO-1001",
            invoiceAmount: 1500m,
            aggregateAcceptedGrnValue: 1200m,
            remainingInvoiceableAmount: 1200m,
            discrepancyReason: "Invoice amount exceeds remaining invoiceable amount.");

        _emailMock.Verify(e => e.SendEmailAsync(
            "supplier@test.com",
            "Invoice INV-1001 — Match Failed for PO-1001",
            It.IsAny<string>(),
            true), Times.Once);

        Assert.Empty(_context.OutboxMessages);
        Assert.Empty(_context.Notifications);
    }

    private OutboxProcessorService CreateOutboxProcessor()
    {
        var services = new ServiceCollection();
        services.AddSingleton<AppDbContext>(_ => _context);
        services.AddScoped<IRealtimeService>(_ => _realtimeMock.Object);
        services.AddScoped<IEmailService>(_ => _emailMock.Object);
        var provider = services.BuildServiceProvider();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", "Host=localhost;Database=smartinventory;Username=test;Password=test")
            })
            .Build();

        return new OutboxProcessorService(
            provider,
            new NullLogger<OutboxProcessorService>(),
            config);
    }

    private async Task RunOutboxProcessorAsync(OutboxProcessorService processor)
    {
        var method = typeof(OutboxProcessorService).GetMethod(
            "ProcessOutboxAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var task = (Task)method!.Invoke(processor, new object[] { CancellationToken.None })!;
        await task;
    }

    public void Dispose()
    {
        _context.Dispose();
        _memoryCache.Dispose();
    }
}
