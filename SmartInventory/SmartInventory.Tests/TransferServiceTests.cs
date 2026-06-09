using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MediatR;
using Moq;
using System.Security.Claims;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Events;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;
using SmartInventory.Repository;
using SmartInventory.Repository.Repositories;
using SmartInventory.Service.Services;

namespace SmartInventory.Tests;

public class TransferServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _uow;
    private readonly TransferService _service;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<INotificationService> _notificationMock = new(MockBehavior.Loose);
    private readonly Mock<ICacheService> _cacheMock = new(MockBehavior.Loose);
    private readonly Mock<IPublisher> _publisherMock = new(MockBehavior.Loose);
    private readonly Mock<IAuthorizationService> _authorizationMock = new(MockBehavior.Loose);
    private readonly Mock<ITransferVarianceResolver> _varianceResolverMock = new(MockBehavior.Loose);

    public TransferServiceTests()
    {
        _context = TestDbContextFactory.Create();

        var products = new Mock<IProductRepository>();
        var suppliers = new Mock<ISupplierRepository>();
        var purchaseOrders = new Mock<IPurchaseOrderRepository>();
        var transfers = new Mock<ITransferRepository>();
        var barcodes = new Mock<IBarcodeRepository>();
        var notifications = new Mock<INotificationRepository>();
        var stockLevels = new Mock<IStockLevelRepository>();

        _uow = new UnitOfWork(
            _context,
            products.Object,
            suppliers.Object,
            purchaseOrders.Object,
            transfers.Object,
            barcodes.Object,
            notifications.Object,
            stockLevels.Object);

        _currentUserServiceMock.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _currentUserServiceMock.Setup(x => x.Role).Returns("Staff");
        _currentUserServiceMock.Setup(x => x.WarehouseId).Returns((Guid?)null);
        _currentUserServiceMock.Setup(x => x.IpAddress).Returns((string?)null);
        _currentUserServiceMock.Setup(x => x.Principal).Returns((System.Security.Claims.ClaimsPrincipal?)null);
        _currentUserServiceMock.Setup(x => x.IsInRole(It.IsAny<string[]>()))
            .Returns(false);

        _cacheMock.Setup(c => c.GetAsync<TransferResponseDto>(It.IsAny<string>()))
            .ReturnsAsync((TransferResponseDto?)null);
        _cacheMock.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<TransferResponseDto>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);
        _varianceResolverMock.Setup(v => v.NotifyVarianceCreatedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        _varianceResolverMock.Setup(v => v.TryResolveTransferVarianceAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        _authorizationMock.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Failed());

        _service = new TransferService(
            _uow,
            _notificationMock.Object,
            _cacheMock.Object,
            _currentUserServiceMock.Object,
            _publisherMock.Object,
            _authorizationMock.Object,
            _varianceResolverMock.Object);
    }

    [Fact]
    public async Task TransferBinToBinAsync_WhenDestinationStockMissing_InsertsStockLevelAndPreservesSideEffects()
    {
        var seed = await SeedAsync(includeDestinationStock: false);
        var beforeOutboxCount = await _context.OutboxMessages.CountAsync();
        var beforeAuditCount = await _context.AuditLogs.CountAsync();

        var result = await _service.TransferBinToBinAsync(new BinTransferCreateDto
        {
            WarehouseId = seed.Warehouse.Id,
            ProductId = seed.Product.Id,
            FromBinId = seed.FromBin.Id,
            ToBinId = seed.ToBin.Id,
            Quantity = 3,
            Reason = "Bin shuffle"
        }, performedBy: seed.PerformedBy);

        Assert.True(result);

        var sourceStock = await _context.StockLevels.SingleAsync(sl =>
            sl.ProductId == seed.Product.Id &&
            sl.WarehouseId == seed.Warehouse.Id &&
            sl.BinLocationId == seed.FromBin.Id);

        var destinationStock = await _context.StockLevels.SingleAsync(sl =>
            sl.ProductId == seed.Product.Id &&
            sl.WarehouseId == seed.Warehouse.Id &&
            sl.BinLocationId == seed.ToBin.Id);

        Assert.Equal(7, sourceStock.QuantityOnHand);
        Assert.Equal(3, destinationStock.QuantityOnHand);
        Assert.Equal(0, destinationStock.QuantityInTransit);

        var updatedToBin = await _context.BinLocations.SingleAsync(b => b.Id == seed.ToBin.Id);
        Assert.Equal(90m, updatedToBin.UtilizedVolumeCm3);
        Assert.Equal(6m, updatedToBin.UtilizedWeightKg);

        Assert.Equal(beforeOutboxCount + 2, await _context.OutboxMessages.CountAsync());

        var newAuditLogs = await _context.AuditLogs
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Skip(beforeAuditCount)
            .ToListAsync();

        Assert.NotEmpty(newAuditLogs);
        Assert.Contains(newAuditLogs, a => a.EntityType == nameof(StockLevel));
        Assert.Contains(newAuditLogs, a => a.EntityType == nameof(OutboxMessage));
        Assert.Contains(newAuditLogs, a => a.EntityType == nameof(StockMovement));
        Assert.True(await _context.AuditLogs.CountAsync() >= beforeAuditCount + 6);

        Assert.Equal(2, await _context.StockMovements.CountAsync(m =>
            m.ReferenceType == ReferenceType.Transfer &&
            m.ReferenceId == Guid.Empty &&
            m.ProductId == seed.Product.Id));
    }

    [Fact]
    public async Task TransferBinToBinAsync_WhenDestinationStockExists_UpdatesExistingStockLevel()
    {
        var seed = await SeedAsync(includeDestinationStock: true);
        var beforeOutboxCount = await _context.OutboxMessages.CountAsync();
        var beforeAuditCount = await _context.AuditLogs.CountAsync();

        var result = await _service.TransferBinToBinAsync(new BinTransferCreateDto
        {
            WarehouseId = seed.Warehouse.Id,
            ProductId = seed.Product.Id,
            FromBinId = seed.FromBin.Id,
            ToBinId = seed.ToBin.Id,
            Quantity = 3,
            Reason = "Bin shuffle"
        }, performedBy: seed.PerformedBy);

        Assert.True(result);

        var sourceStock = await _context.StockLevels.SingleAsync(sl =>
            sl.ProductId == seed.Product.Id &&
            sl.WarehouseId == seed.Warehouse.Id &&
            sl.BinLocationId == seed.FromBin.Id);

        var destinationStock = await _context.StockLevels.SingleAsync(sl =>
            sl.ProductId == seed.Product.Id &&
            sl.WarehouseId == seed.Warehouse.Id &&
            sl.BinLocationId == seed.ToBin.Id);

        Assert.Equal(7, sourceStock.QuantityOnHand);
        Assert.Equal(8, destinationStock.QuantityOnHand);
        Assert.Equal(0, destinationStock.QuantityInTransit);

        var updatedToBin = await _context.BinLocations.SingleAsync(b => b.Id == seed.ToBin.Id);
        Assert.Equal(90m, updatedToBin.UtilizedVolumeCm3);
        Assert.Equal(6m, updatedToBin.UtilizedWeightKg);

        Assert.Equal(beforeOutboxCount + 2, await _context.OutboxMessages.CountAsync());
        Assert.True(await _context.AuditLogs.CountAsync() >= beforeAuditCount + 6);
    }

    [Fact]
    public async Task TransferBinToBinAsync_WhenCapacityExceeded_ThrowsBeforeStockMutation()
    {
        var seed = await SeedAsync(includeDestinationStock: false, destinationBinMaxVolume: 20m);
        var beforeOutboxCount = await _context.OutboxMessages.CountAsync();
        var beforeAuditCount = await _context.AuditLogs.CountAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.TransferBinToBinAsync(new BinTransferCreateDto
        {
            WarehouseId = seed.Warehouse.Id,
            ProductId = seed.Product.Id,
            FromBinId = seed.FromBin.Id,
            ToBinId = seed.ToBin.Id,
            Quantity = 3,
            Reason = "Too large"
        }, performedBy: seed.PerformedBy));

        var sourceStock = await _context.StockLevels.SingleAsync(sl =>
            sl.ProductId == seed.Product.Id &&
            sl.WarehouseId == seed.Warehouse.Id &&
            sl.BinLocationId == seed.FromBin.Id);

        Assert.Equal(10, sourceStock.QuantityOnHand);
        Assert.Equal(beforeOutboxCount, await _context.OutboxMessages.CountAsync());
        Assert.Equal(beforeAuditCount, await _context.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task TransferBinToBinAsync_WhenSourceStockMissing_ThrowsNotFoundException()
    {
        var seed = await SeedAsync(includeDestinationStock: false);
        var sourceStock = await _context.StockLevels.SingleAsync(sl =>
            sl.ProductId == seed.Product.Id &&
            sl.WarehouseId == seed.Warehouse.Id &&
            sl.BinLocationId == seed.FromBin.Id);

        _context.StockLevels.Remove(sourceStock);
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _service.TransferBinToBinAsync(new BinTransferCreateDto
        {
            WarehouseId = seed.Warehouse.Id,
            ProductId = seed.Product.Id,
            FromBinId = seed.FromBin.Id,
            ToBinId = seed.ToBin.Id,
            Quantity = 1,
            Reason = "Missing source"
        }, performedBy: seed.PerformedBy));
    }

    [Fact]
    public async Task TransferBinToBinAsync_WhenAvailableStockIsInsufficient_ThrowsBeforeMutation()
    {
        var seed = await SeedAsync(includeDestinationStock: false);
        var sourceStock = await _context.StockLevels.SingleAsync(sl =>
            sl.ProductId == seed.Product.Id &&
            sl.WarehouseId == seed.Warehouse.Id &&
            sl.BinLocationId == seed.FromBin.Id);

        sourceStock.QuantityReserved = sourceStock.QuantityOnHand;
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<InsufficientStockException>(() => _service.TransferBinToBinAsync(new BinTransferCreateDto
        {
            WarehouseId = seed.Warehouse.Id,
            ProductId = seed.Product.Id,
            FromBinId = seed.FromBin.Id,
            ToBinId = seed.ToBin.Id,
            Quantity = 1,
            Reason = "No available stock"
        }, performedBy: seed.PerformedBy));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task TransferBinToBinAsync_ValidationFailures_ThrowExpectedExceptions(int scenario)
    {
        switch ((BinTransferValidationScenario)scenario)
        {
            case BinTransferValidationScenario.SameBin:
                var binId = Guid.NewGuid();
                await Assert.ThrowsAsync<BusinessRuleException>(() => _service.TransferBinToBinAsync(new BinTransferCreateDto
                {
                    WarehouseId = Guid.NewGuid(),
                    ProductId = Guid.NewGuid(),
                    FromBinId = binId,
                    ToBinId = binId,
                    Quantity = 1,
                    Reason = "Same bin"
                }, performedBy: Guid.NewGuid()));
                break;

            case BinTransferValidationScenario.NonPositiveQuantity:
                await Assert.ThrowsAsync<BusinessRuleException>(() => _service.TransferBinToBinAsync(new BinTransferCreateDto
                {
                    WarehouseId = Guid.NewGuid(),
                    ProductId = Guid.NewGuid(),
                    FromBinId = Guid.NewGuid(),
                    ToBinId = Guid.NewGuid(),
                    Quantity = 0,
                    Reason = "Zero quantity"
                }, performedBy: Guid.NewGuid()));
                break;
        }
    }

    [Fact]
    public async Task TransferBinToBinAsync_WhenWarningsAreNotBypassed_ThrowsBeforeMutation()
    {
        var seed = await SeedAsync(includeDestinationStock: false);
        _currentUserServiceMock.Setup(c => c.Principal).Returns(new ClaimsPrincipal(new ClaimsIdentity("test")));

        var toZone = await _context.WarehouseZones.SingleAsync(z => z.Id == seed.ToBin.ZoneId);
        toZone.ZoneType = ZoneType.Receiving;
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.TransferBinToBinAsync(new BinTransferCreateDto
        {
            WarehouseId = seed.Warehouse.Id,
            ProductId = seed.Product.Id,
            FromBinId = seed.FromBin.Id,
            ToBinId = seed.ToBin.Id,
            Quantity = 1,
            Reason = "Warning path",
            BypassWarnings = false
        }, performedBy: seed.PerformedBy));
    }

    [Fact]
    public async Task TransferBinToBinAsync_WhenWarningsAreBypassed_WithoutAuthorization_ThrowsUnauthorizedAccessException()
    {
        var seed = await SeedAsync(includeDestinationStock: false);
        _currentUserServiceMock.Setup(c => c.Principal).Returns((ClaimsPrincipal?)null);

        var toZone = await _context.WarehouseZones.SingleAsync(z => z.Id == seed.ToBin.ZoneId);
        toZone.ZoneType = ZoneType.Receiving;
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.TransferBinToBinAsync(new BinTransferCreateDto
        {
            WarehouseId = seed.Warehouse.Id,
            ProductId = seed.Product.Id,
            FromBinId = seed.FromBin.Id,
            ToBinId = seed.ToBin.Id,
            Quantity = 1,
            Reason = "Unauthorized override",
            BypassWarnings = true
        }, performedBy: seed.PerformedBy));
    }

    [Fact]
    public async Task TransferBinToBinAsync_WhenTargetBinCrossesThreshold_PublishesCapacityThresholdEvent()
    {
        var seed = await SeedAsync(includeDestinationStock: false, destinationBinMaxVolume: 100m);
        var toBin = await _context.BinLocations.SingleAsync(b => b.Id == seed.ToBin.Id);
        toBin.UtilizedVolumeCm3 = 61m;
        await _context.SaveChangesAsync();

        var result = await _service.TransferBinToBinAsync(new BinTransferCreateDto
        {
            WarehouseId = seed.Warehouse.Id,
            ProductId = seed.Product.Id,
            FromBinId = seed.FromBin.Id,
            ToBinId = seed.ToBin.Id,
            Quantity = 1,
            Reason = "Threshold test"
        }, performedBy: seed.PerformedBy);

        Assert.True(result);
        _publisherMock.Verify(p => p.Publish(
            It.Is<BinCapacityThresholdReachedEvent>(e => e.BinId == seed.ToBin.Id && e.UtilizationPercentage > 90m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task TransferBinToBinAsync_WhenOriginBinUtilizationIsCorrupt_ThrowsBusinessRuleException(int scenario)
    {
        var seed = scenario == 0
            ? await SeedAsync(includeDestinationStock: false)
            : await SeedAsync(includeDestinationStock: false);

        var fromBin = await _context.BinLocations.SingleAsync(b => b.Id == seed.FromBin.Id);
        if (scenario == 0)
            fromBin.UtilizedVolumeCm3 = 80m;
        else
            fromBin.UtilizedWeightKg = 5m;

        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.TransferBinToBinAsync(new BinTransferCreateDto
        {
            WarehouseId = seed.Warehouse.Id,
            ProductId = seed.Product.Id,
            FromBinId = seed.FromBin.Id,
            ToBinId = seed.ToBin.Id,
            Quantity = 3,
            Reason = "Corruption guard"
        }, performedBy: seed.PerformedBy));
    }

    [Fact]
    public async Task TransferBinToBinAsync_WhenBinTypeIsMismatched_UsesDefaultOverrideReason()
    {
        var seed = await SeedAsync(includeDestinationStock: false);
        _currentUserServiceMock.Setup(c => c.Principal).Returns(new ClaimsPrincipal(new ClaimsIdentity("test")));
        _authorizationMock.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var product = await _context.Products.SingleAsync(p => p.Id == seed.Product.Id);
        product.PreferredBinType = BinType.Fragile;
        await _context.SaveChangesAsync();

        var result = await _service.TransferBinToBinAsync(new BinTransferCreateDto
        {
            WarehouseId = seed.Warehouse.Id,
            ProductId = seed.Product.Id,
            FromBinId = seed.FromBin.Id,
            ToBinId = seed.ToBin.Id,
            Quantity = 1,
            Reason = "Bin type override",
            BypassWarnings = true
        }, performedBy: seed.PerformedBy);

        Assert.True(result);
        _publisherMock.Verify(p => p.Publish(
            It.Is<CapacityOverridePerformedEvent>(e =>
                e.RuleBroken == "BinTypeMismatch" &&
                e.OverrideReason == "Manual Override" &&
                e.ProductId == seed.Product.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransferBinToBinAsync_WhenWarningsAreBypassed_PublishesOverrideEvent()
    {
        var seed = await SeedAsync(includeDestinationStock: false);
        _currentUserServiceMock.Setup(c => c.Principal).Returns(new ClaimsPrincipal(new ClaimsIdentity("test")));
        _authorizationMock.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var toZone = await _context.WarehouseZones.SingleAsync(z => z.Id == seed.ToBin.ZoneId);
        toZone.ZoneType = ZoneType.Receiving;
        await _context.SaveChangesAsync();

        var beforeAuditCount = await _context.AuditLogs.CountAsync();

        var result = await _service.TransferBinToBinAsync(new BinTransferCreateDto
        {
            WarehouseId = seed.Warehouse.Id,
            ProductId = seed.Product.Id,
            FromBinId = seed.FromBin.Id,
            ToBinId = seed.ToBin.Id,
            Quantity = 2,
            Reason = "Override putaway",
            BypassWarnings = true,
            OverrideReason = "Approved override"
        }, performedBy: seed.PerformedBy);

        Assert.True(result);
        _publisherMock.Verify(p => p.Publish(
            It.Is<CapacityOverridePerformedEvent>(e =>
                e.RuleBroken == "ZoneMismatch" &&
                e.ProductId == seed.Product.Id &&
                e.OverrideReason == "Approved override"),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(await _context.AuditLogs.CountAsync() > beforeAuditCount);
    }

    [Fact]
    public async Task CreateTransferAsync_ReturnsCachedResponse_WhenIdempotencyKeyHits()
    {
        var cached = new TransferResponseDto
        {
            Id = Guid.NewGuid(),
            TransferNumber = "TRF-CACHED",
            Status = TransferStatus.Requested
        };

        _cacheMock.Setup(c => c.GetAsync<TransferResponseDto>("Idempotency_Transfer_CACHE-1"))
            .ReturnsAsync(cached);

        var result = await _service.CreateTransferAsync(new TransferCreateDto
        {
            FromWarehouseId = Guid.NewGuid(),
            ToWarehouseId = Guid.NewGuid(),
            RequestedBy = Guid.NewGuid(),
            IdempotencyKey = "CACHE-1"
        });

        Assert.Same(cached, result);
        _cacheMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<TransferResponseDto>(), It.IsAny<TimeSpan?>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task CreateTransferAsync_ValidationFailures_ThrowExpectedExceptions(int scenario)
    {
        switch ((CreateTransferValidationScenario)scenario)
        {
            case CreateTransferValidationScenario.SameWarehouse:
            {
                var warehouseId = Guid.NewGuid();
                await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateTransferAsync(new TransferCreateDto
                {
                    FromWarehouseId = warehouseId,
                    ToWarehouseId = warehouseId,
                    RequestedBy = Guid.NewGuid(),
                    Items = new List<TransferItemDto>()
                }));
                break;
            }

            case CreateTransferValidationScenario.MissingOriginWarehouse:
            {
                await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateTransferAsync(new TransferCreateDto
                {
                    FromWarehouseId = Guid.NewGuid(),
                    ToWarehouseId = Guid.NewGuid(),
                    RequestedBy = Guid.NewGuid(),
                    Items = [new TransferItemDto { ProductId = Guid.NewGuid(), FromBinId = Guid.NewGuid(), ToBinId = Guid.NewGuid(), QuantityRequested = 1 }]
                }));
                break;
            }

            case CreateTransferValidationScenario.MissingDestinationWarehouse:
            {
                var seed = await SeedWarehouseTransferAsync();
                await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateTransferAsync(new TransferCreateDto
                {
                    FromWarehouseId = seed.FromWarehouse.Id,
                    ToWarehouseId = Guid.NewGuid(),
                    RequestedBy = seed.Requester.Id,
                    Items = [new TransferItemDto { ProductId = seed.Product.Id, FromBinId = seed.FromBin.Id, ToBinId = seed.ToBin.Id, QuantityRequested = 1 }]
                }));
                break;
            }

            case CreateTransferValidationScenario.MissingRequester:
            {
                var seed = await SeedWarehouseTransferAsync();
                await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateTransferAsync(new TransferCreateDto
                {
                    FromWarehouseId = seed.FromWarehouse.Id,
                    ToWarehouseId = seed.ToWarehouse.Id,
                    RequestedBy = Guid.NewGuid(),
                    Items = [new TransferItemDto { ProductId = seed.Product.Id, FromBinId = seed.FromBin.Id, ToBinId = seed.ToBin.Id, QuantityRequested = 1 }]
                }));
                break;
            }

            case CreateTransferValidationScenario.NonPositiveQuantity:
            {
                var seed = await SeedWarehouseTransferAsync();
                await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateTransferAsync(new TransferCreateDto
                {
                    FromWarehouseId = seed.FromWarehouse.Id,
                    ToWarehouseId = seed.ToWarehouse.Id,
                    RequestedBy = seed.Requester.Id,
                    Items = [new TransferItemDto { ProductId = seed.Product.Id, FromBinId = seed.FromBin.Id, ToBinId = seed.ToBin.Id, QuantityRequested = 0 }]
                }));
                break;
            }

            case CreateTransferValidationScenario.MissingProduct:
            {
                var seed = await SeedWarehouseTransferAsync();
                await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateTransferAsync(new TransferCreateDto
                {
                    FromWarehouseId = seed.FromWarehouse.Id,
                    ToWarehouseId = seed.ToWarehouse.Id,
                    RequestedBy = seed.Requester.Id,
                    Items = [new TransferItemDto { ProductId = Guid.NewGuid(), FromBinId = seed.FromBin.Id, ToBinId = seed.ToBin.Id, QuantityRequested = 1 }]
                }));
                break;
            }

            case CreateTransferValidationScenario.InsufficientStock:
            {
                var seed = await SeedWarehouseTransferAsync(sourceQuantity: 1);
                await Assert.ThrowsAsync<InsufficientStockException>(() => _service.CreateTransferAsync(new TransferCreateDto
                {
                    FromWarehouseId = seed.FromWarehouse.Id,
                    ToWarehouseId = seed.ToWarehouse.Id,
                    RequestedBy = seed.Requester.Id,
                    Items = [new TransferItemDto { ProductId = seed.Product.Id, FromBinId = seed.FromBin.Id, ToBinId = seed.ToBin.Id, QuantityRequested = 2 }]
                }));
                break;
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateTransferAsync_CreatesTransferAndUpdatesReservations(bool destinationHasManager)
    {
        var seed = await SeedWarehouseTransferAsync(destinationManager: destinationHasManager);
        var dto = new TransferCreateDto
        {
            FromWarehouseId = seed.FromWarehouse.Id,
            ToWarehouseId = seed.ToWarehouse.Id,
            RequestedBy = seed.Requester.Id,
            Notes = "Test transfer",
            IdempotencyKey = destinationHasManager ? "CREATE-MGR" : "CREATE-NOMGR",
            Items =
            [
                new TransferItemDto
                {
                    ProductId = seed.Product.Id,
                    FromBinId = seed.FromBin.Id,
                    ToBinId = seed.ToBin.Id,
                    QuantityRequested = 3
                }
            ]
        };

        var result = await _service.CreateTransferAsync(dto);

        Assert.Equal(TransferStatus.Requested, result.Status);
        Assert.Equal(seed.FromWarehouse.Id, result.FromWarehouseId);
        Assert.Equal(seed.ToWarehouse.Id, result.ToWarehouseId);
        Assert.Single(result.Items);
        Assert.Equal(3, result.Items[0].QuantityRequested);

        var reservedStock = await _context.StockLevels.SingleAsync(sl =>
            sl.ProductId == seed.Product.Id &&
            sl.WarehouseId == seed.FromWarehouse.Id &&
            sl.BinLocationId == seed.FromBin.Id);

        Assert.Equal(3, reservedStock.QuantityReserved);
        _cacheMock.Verify(c => c.SetAsync($"Idempotency_Transfer_{dto.IdempotencyKey}", It.IsAny<TransferResponseDto>(), It.IsAny<TimeSpan?>()), Times.Once);

        if (destinationHasManager)
        {
            _notificationMock.Verify(n => n.SendNotificationAsync(seed.ToWarehouse.ManagerId!.Value, NotificationChannel.InApp,
                "TransferRequested", It.IsAny<string>(), It.IsAny<string>(), "WarehouseTransfer", It.IsAny<Guid?>()), Times.Once);
        }
        else
        {
            _notificationMock.Verify(n => n.SendNotificationAsync(It.IsAny<Guid>(), It.IsAny<NotificationChannel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>()), Times.Never);
        }
    }

    [Fact]
    public async Task CreateTransferAsync_WhenCommitThrowsConcurrencyException_ThrowsStaleDataException()
    {
        var seed = await SeedWarehouseTransferAsync();
        var throwingService = new TransferService(
            new ThrowingCommitUnitOfWork(_uow),
            _notificationMock.Object,
            _cacheMock.Object,
            _currentUserServiceMock.Object,
            _publisherMock.Object,
            _authorizationMock.Object,
            _varianceResolverMock.Object);

        await Assert.ThrowsAsync<StaleDataException>(() => throwingService.CreateTransferAsync(new TransferCreateDto
        {
            FromWarehouseId = seed.FromWarehouse.Id,
            ToWarehouseId = seed.ToWarehouse.Id,
            RequestedBy = seed.Requester.Id,
            Items =
            [
                new TransferItemDto
                {
                    ProductId = seed.Product.Id,
                    FromBinId = seed.FromBin.Id,
                    ToBinId = seed.ToBin.Id,
                    QuantityRequested = 1
                }
            ]
        }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ApproveTransferAsync_ValidationFailures_ThrowExpectedExceptions(int scenario)
    {
        switch ((ApproveTransferValidationScenario)scenario)
        {
            case ApproveTransferValidationScenario.TransferNotFound:
                await Assert.ThrowsAsync<NotFoundException>(() => _service.ApproveTransferAsync(Guid.NewGuid(), new TransferApprovalDto { ApprovedBy = Guid.NewGuid(), Approve = true }));
                break;

            case ApproveTransferValidationScenario.WrongState:
            {
                var seed = await SeedWarehouseTransferAsync();
                var create = await _service.CreateTransferAsync(new TransferCreateDto
                {
                    FromWarehouseId = seed.FromWarehouse.Id,
                    ToWarehouseId = seed.ToWarehouse.Id,
                    RequestedBy = seed.Requester.Id,
                    Items =
                    [
                        new TransferItemDto { ProductId = seed.Product.Id, FromBinId = seed.FromBin.Id, ToBinId = seed.ToBin.Id, QuantityRequested = 1 }
                    ]
                });

                var transfer = await _context.WarehouseTransfers.SingleAsync(t => t.Id == create.Id);
                transfer.Status = TransferStatus.Approved;
                await _context.SaveChangesAsync();

                await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ApproveTransferAsync(create.Id, new TransferApprovalDto { ApprovedBy = seed.Requester.Id, Approve = true }));
                break;
            }

            case ApproveTransferValidationScenario.ApproverMissing:
            {
                var seed = await SeedWarehouseTransferAsync();
                var create = await _service.CreateTransferAsync(new TransferCreateDto
                {
                    FromWarehouseId = seed.FromWarehouse.Id,
                    ToWarehouseId = seed.ToWarehouse.Id,
                    RequestedBy = seed.Requester.Id,
                    Items =
                    [
                        new TransferItemDto { ProductId = seed.Product.Id, FromBinId = seed.FromBin.Id, ToBinId = seed.ToBin.Id, QuantityRequested = 1 }
                    ]
                });

                await Assert.ThrowsAsync<NotFoundException>(() => _service.ApproveTransferAsync(create.Id, new TransferApprovalDto { ApprovedBy = Guid.NewGuid(), Approve = true }));
                break;
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ApproveTransferAsync_ApproveAndReject_UpdateTransferAndReservations(bool approve)
    {
        var seed = await SeedWarehouseTransferAsync();
        var create = await _service.CreateTransferAsync(new TransferCreateDto
        {
            FromWarehouseId = seed.FromWarehouse.Id,
            ToWarehouseId = seed.ToWarehouse.Id,
            RequestedBy = seed.Requester.Id,
            Items =
            [
                new TransferItemDto { ProductId = seed.Product.Id, FromBinId = seed.FromBin.Id, ToBinId = seed.ToBin.Id, QuantityRequested = 3 }
            ]
        });

        var approval = await _service.ApproveTransferAsync(create.Id, new TransferApprovalDto { ApprovedBy = seed.Requester.Id, Approve = approve });

        Assert.Equal(approve ? TransferStatus.Approved : TransferStatus.Rejected, approval.Status);
        var reservedStock = await _context.StockLevels.SingleAsync(sl => sl.ProductId == seed.Product.Id && sl.WarehouseId == seed.FromWarehouse.Id && sl.BinLocationId == seed.FromBin.Id);
        Assert.Equal(approve ? 3 : 0, reservedStock.QuantityReserved);
    }

    [Fact]
    public async Task ApproveTransferAsync_WhenCommitThrowsConcurrencyException_ThrowsStaleDataException()
    {
        var seed = await SeedWarehouseTransferAsync();
        var create = await _service.CreateTransferAsync(new TransferCreateDto
        {
            FromWarehouseId = seed.FromWarehouse.Id,
            ToWarehouseId = seed.ToWarehouse.Id,
            RequestedBy = seed.Requester.Id,
            Items =
            [
                new TransferItemDto { ProductId = seed.Product.Id, FromBinId = seed.FromBin.Id, ToBinId = seed.ToBin.Id, QuantityRequested = 1 }
            ]
        });

        var throwingService = new TransferService(
            new ThrowingCommitUnitOfWork(_uow),
            _notificationMock.Object,
            _cacheMock.Object,
            _currentUserServiceMock.Object,
            _publisherMock.Object,
            _authorizationMock.Object,
            _varianceResolverMock.Object);

        await Assert.ThrowsAsync<StaleDataException>(() => throwingService.ApproveTransferAsync(create.Id, new TransferApprovalDto { ApprovedBy = seed.Requester.Id, Approve = true }));
    }

    [Fact]
    public async Task DispatchTransferAsync_ProcessesMixedItems_AndRaisesStockAlerts()
    {
        var seed1 = await SeedWarehouseTransferAsync(
            destinationManager: true,
            sourceQuantity: 3,
            safetyStockQty: 10,
            reorderPoint: 20,
            fromBinUtilizedVolume: 10m,
            fromBinUtilizedWeight: 5m,
            toBinUtilizedVolume: 88m,
            toBinMaxVolume: 100m,
            length: 1m,
            width: 1m,
            height: 1m,
            weight: 1m);

        var role = await _context.Roles.FirstAsync(r => r.Name == "Staff");
        var category = new Category { Id = Guid.NewGuid(), Name = "Hardware-2", Description = "Tools" };
        var product2 = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Safety Widget",
            SKU = "TRF-002",
            CostPrice = 10m,
            SellingPrice = 15m,
            ReorderPoint = 10,
            SafetyStockQty = 5,
            IsActive = true,
            CategoryId = category.Id,
            Category = category,
            Length = 2m,
            Width = 2m,
            Height = 2m,
            WeightKg = 1m,
            PreferredBinType = BinType.Standard
        };
        var productLow = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Low Widget",
            SKU = "TRF-003",
            CostPrice = 10m,
            SellingPrice = 15m,
            ReorderPoint = 10,
            SafetyStockQty = 2,
            IsActive = true,
            CategoryId = category.Id,
            Category = category,
            Length = 2m,
            Width = 2m,
            Height = 2m,
            WeightKg = 1m,
            PreferredBinType = BinType.Standard
        };
        var fromZone2 = new WarehouseZone
        {
            Id = Guid.NewGuid(),
            Name = "From Zone 2",
            Code = "FZ-2",
            ZoneType = ZoneType.Storage,
            WarehouseId = seed1.FromWarehouse.Id,
            AreaSqFt = 50,
            MaxVolumeCm3 = 1000,
            MaxWeightKg = 1000
        };
        var toZone2 = new WarehouseZone
        {
            Id = Guid.NewGuid(),
            Name = "To Zone 2",
            Code = "TZ-2",
            ZoneType = ZoneType.Storage,
            WarehouseId = seed1.ToWarehouse.Id,
            AreaSqFt = 50,
            MaxVolumeCm3 = 1000,
            MaxWeightKg = 1000
        };
        var fromBin2 = new BinLocation
        {
            Id = Guid.NewGuid(),
            BinCode = "FROM-02",
            ZoneId = fromZone2.Id,
            Zone = fromZone2,
            BinType = BinType.Standard,
            MaxVolumeCm3 = 1000,
            MaxWeightKg = 1000,
            UtilizedVolumeCm3 = 20m,
            UtilizedWeightKg = 5m
        };
        var toBin2 = new BinLocation
        {
            Id = Guid.NewGuid(),
            BinCode = "TO-02",
            ZoneId = toZone2.Id,
            Zone = toZone2,
            BinType = BinType.Standard,
            MaxVolumeCm3 = 1000,
            MaxWeightKg = 1000,
            UtilizedVolumeCm3 = 0m,
            UtilizedWeightKg = 0m
        };
        var fromStock2 = new StockLevel
        {
            Id = Guid.NewGuid(),
            ProductId = product2.Id,
            WarehouseId = seed1.FromWarehouse.Id,
            BinLocationId = fromBin2.Id,
            QuantityOnHand = 6,
            QuantityReserved = 0,
            QuantityOnOrder = 0,
            QuantityInTransit = 0,
            LastUpdated = DateTime.UtcNow
        };
        var destStock2 = new StockLevel
        {
            Id = Guid.NewGuid(),
            ProductId = product2.Id,
            WarehouseId = seed1.ToWarehouse.Id,
            BinLocationId = toBin2.Id,
            QuantityOnHand = 1,
            QuantityReserved = 0,
            QuantityOnOrder = 0,
            QuantityInTransit = 0,
            LastUpdated = DateTime.UtcNow
        };

        var category3 = new Category { Id = Guid.NewGuid(), Name = "Hardware-3", Description = "Tools" };
        var product3WarehouseBin = new BinLocation
        {
            Id = Guid.NewGuid(),
            BinCode = "TO-03",
            ZoneId = seed1.ToZone.Id,
            Zone = seed1.ToZone,
            BinType = BinType.Standard,
            MaxVolumeCm3 = 1000,
            MaxWeightKg = 1000,
            UtilizedVolumeCm3 = 0m,
            UtilizedWeightKg = 0m
        };
        var fromStock3 = new StockLevel
        {
            Id = Guid.NewGuid(),
            ProductId = productLow.Id,
            WarehouseId = seed1.FromWarehouse.Id,
            BinLocationId = null,
            QuantityOnHand = 11,
            QuantityReserved = 0,
            QuantityOnOrder = 0,
            QuantityInTransit = 0,
            LastUpdated = DateTime.UtcNow
        };

        await _context.Categories.AddAsync(category);
        await _context.Categories.AddAsync(category3);
        await _context.Products.AddAsync(product2);
        await _context.Products.AddAsync(productLow);
        await _context.WarehouseZones.AddAsync(fromZone2);
        await _context.WarehouseZones.AddAsync(toZone2);
        await _context.BinLocations.AddAsync(fromBin2);
        await _context.BinLocations.AddAsync(toBin2);
        await _context.BinLocations.AddAsync(product3WarehouseBin);
        await _context.StockLevels.AddAsync(fromStock2);
        await _context.StockLevels.AddAsync(destStock2);
        await _context.StockLevels.AddAsync(fromStock3);
        await _context.SaveChangesAsync();

        var transfer = await _service.CreateTransferAsync(new TransferCreateDto
        {
            FromWarehouseId = seed1.FromWarehouse.Id,
            ToWarehouseId = seed1.ToWarehouse.Id,
            RequestedBy = seed1.Requester.Id,
            Items =
            [
                new TransferItemDto
                {
                    ProductId = seed1.Product.Id,
                    FromBinId = seed1.FromBin.Id,
                    ToBinId = seed1.ToBin.Id,
                    QuantityRequested = 3
                },
                new TransferItemDto
                {
                    ProductId = product2.Id,
                    FromBinId = fromBin2.Id,
                    ToBinId = toBin2.Id,
                    QuantityRequested = 2
                },
                new TransferItemDto
                {
                    ProductId = productLow.Id,
                    FromBinId = null,
                    ToBinId = product3WarehouseBin.Id,
                    QuantityRequested = 3
                }
            ]
        });

        await _service.ApproveTransferAsync(transfer.Id, new TransferApprovalDto { ApprovedBy = seed1.Requester.Id, Approve = true });

        var dispatched = await _service.DispatchTransferAsync(transfer.Id, seed1.Requester.Id);

        Assert.Equal(TransferStatus.InTransit, dispatched.Status);

        var source1 = await _context.StockLevels.SingleAsync(sl => sl.ProductId == seed1.Product.Id && sl.WarehouseId == seed1.FromWarehouse.Id && sl.BinLocationId == seed1.FromBin.Id);
        var source2 = await _context.StockLevels.SingleAsync(sl => sl.ProductId == product2.Id && sl.WarehouseId == seed1.FromWarehouse.Id && sl.BinLocationId == fromBin2.Id);
        var source3 = await _context.StockLevels.SingleAsync(sl => sl.ProductId == productLow.Id && sl.WarehouseId == seed1.FromWarehouse.Id && sl.BinLocationId == null);
        Assert.Equal(0, source1.QuantityOnHand);
        Assert.Equal(4, source2.QuantityOnHand);
        Assert.Equal(8, source3.QuantityOnHand);

        var dest1 = await _context.StockLevels.SingleAsync(sl => sl.ProductId == seed1.Product.Id && sl.WarehouseId == seed1.ToWarehouse.Id && sl.BinLocationId == seed1.ToBin.Id);
        var dest2 = await _context.StockLevels.SingleAsync(sl => sl.ProductId == product2.Id && sl.WarehouseId == seed1.ToWarehouse.Id && sl.BinLocationId == toBin2.Id);
        var dest3 = await _context.StockLevels.SingleAsync(sl => sl.ProductId == productLow.Id && sl.WarehouseId == seed1.ToWarehouse.Id && sl.BinLocationId == product3WarehouseBin.Id);
        Assert.Equal(3, dest1.QuantityInTransit);
        Assert.Equal(2, dest2.QuantityInTransit);
        Assert.Equal(3, dest3.QuantityInTransit);

        _notificationMock.Verify(n => n.SendOutOfStockAlertAsync(seed1.Product.Id, seed1.FromWarehouse.Id, 0), Times.Once);
        _notificationMock.Verify(n => n.SendSafetyStockAlertAsync(product2.Id, seed1.FromWarehouse.Id, 4, 5), Times.Once);
        _notificationMock.Verify(n => n.SendLowStockAlertAsync(productLow.Id, seed1.FromWarehouse.Id, 8, 10), Times.Once);
        _notificationMock.Verify(n => n.SendNotificationAsync(seed1.ToWarehouse.ManagerId!.Value, NotificationChannel.InApp, "TransferDispatched", It.IsAny<string>(), It.IsAny<string>(), "WarehouseTransfer", transfer.Id), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task DispatchTransferAsync_ValidationFailures_ThrowExpectedExceptions(int scenario)
    {
        switch ((DispatchTransferValidationScenario)scenario)
        {
            case DispatchTransferValidationScenario.TransferNotFound:
                await Assert.ThrowsAsync<NotFoundException>(() => _service.DispatchTransferAsync(Guid.NewGuid(), Guid.NewGuid()));
                break;

            case DispatchTransferValidationScenario.WrongState:
            {
                var seed = await SeedWarehouseTransferAsync();
                var transfer = await _service.CreateTransferAsync(new TransferCreateDto
                {
                    FromWarehouseId = seed.FromWarehouse.Id,
                    ToWarehouseId = seed.ToWarehouse.Id,
                    RequestedBy = seed.Requester.Id,
                    Items =
                    [
                        new TransferItemDto { ProductId = seed.Product.Id, FromBinId = seed.FromBin.Id, ToBinId = seed.ToBin.Id, QuantityRequested = 1 }
                    ]
                });

                await Assert.ThrowsAsync<BusinessRuleException>(() => _service.DispatchTransferAsync(transfer.Id, seed.Requester.Id));
                break;
            }
        }
    }

    [Fact]
    public async Task DispatchTransferAsync_WhenSourceStockIsMissing_UpdatesExistingDestinationStock()
    {
        var seed = await SeedWarehouseTransferAsync(includeDestinationStock: true, destinationManager: false);
        var transfer = await _service.CreateTransferAsync(new TransferCreateDto
        {
            FromWarehouseId = seed.FromWarehouse.Id,
            ToWarehouseId = seed.ToWarehouse.Id,
            RequestedBy = seed.Requester.Id,
            Items =
            [
                new TransferItemDto { ProductId = seed.Product.Id, FromBinId = seed.FromBin.Id, ToBinId = seed.ToBin.Id, QuantityRequested = 2 }
            ]
        });

        await _service.ApproveTransferAsync(transfer.Id, new TransferApprovalDto { ApprovedBy = seed.Requester.Id, Approve = true });

        var sourceStock = await _context.StockLevels.SingleAsync(sl => sl.ProductId == seed.Product.Id && sl.WarehouseId == seed.FromWarehouse.Id && sl.BinLocationId == seed.FromBin.Id);
        _context.StockLevels.Remove(sourceStock);
        await _context.SaveChangesAsync();

        var dispatched = await _service.DispatchTransferAsync(transfer.Id, seed.Requester.Id);

        Assert.Equal(TransferStatus.InTransit, dispatched.Status);

        var destinationStock = await _context.StockLevels.SingleAsync(sl => sl.ProductId == seed.Product.Id && sl.WarehouseId == seed.ToWarehouse.Id && sl.BinLocationId == seed.ToBin.Id);
        Assert.Equal(0, destinationStock.QuantityOnHand);
        Assert.Equal(2, destinationStock.QuantityInTransit);
        _notificationMock.Verify(n => n.SendNotificationAsync(It.IsAny<Guid>(), NotificationChannel.InApp, "TransferDispatched", It.IsAny<string>(), It.IsAny<string>(), "WarehouseTransfer", transfer.Id), Times.Never);
    }

    [Fact]
    public async Task DispatchTransferAsync_WhenSourceStockWouldGoNegative_ThrowsInsufficientStockException()
    {
        var seed = await SeedWarehouseTransferAsync(destinationManager: false, sourceQuantity: 5);
        var transfer = await _service.CreateTransferAsync(new TransferCreateDto
        {
            FromWarehouseId = seed.FromWarehouse.Id,
            ToWarehouseId = seed.ToWarehouse.Id,
            RequestedBy = seed.Requester.Id,
            Items =
            [
                new TransferItemDto { ProductId = seed.Product.Id, FromBinId = seed.FromBin.Id, ToBinId = seed.ToBin.Id, QuantityRequested = 3 }
            ]
        });

        await _service.ApproveTransferAsync(transfer.Id, new TransferApprovalDto { ApprovedBy = seed.Requester.Id, Approve = true });

        var sourceStock = await _context.StockLevels.SingleAsync(sl => sl.ProductId == seed.Product.Id && sl.WarehouseId == seed.FromWarehouse.Id && sl.BinLocationId == seed.FromBin.Id);
        sourceStock.QuantityOnHand = 2;
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<InsufficientStockException>(() => _service.DispatchTransferAsync(transfer.Id, seed.Requester.Id));
    }

    [Fact]
    public async Task DispatchTransferAsync_WhenReservedStockWouldGoNegative_ThrowsBusinessRuleException()
    {
        var seed = await SeedWarehouseTransferAsync(destinationManager: false, sourceQuantity: 5);
        var transfer = await _service.CreateTransferAsync(new TransferCreateDto
        {
            FromWarehouseId = seed.FromWarehouse.Id,
            ToWarehouseId = seed.ToWarehouse.Id,
            RequestedBy = seed.Requester.Id,
            Items =
            [
                new TransferItemDto { ProductId = seed.Product.Id, FromBinId = seed.FromBin.Id, ToBinId = seed.ToBin.Id, QuantityRequested = 3 }
            ]
        });

        await _service.ApproveTransferAsync(transfer.Id, new TransferApprovalDto { ApprovedBy = seed.Requester.Id, Approve = true });

        var sourceStock = await _context.StockLevels.SingleAsync(sl => sl.ProductId == seed.Product.Id && sl.WarehouseId == seed.FromWarehouse.Id && sl.BinLocationId == seed.FromBin.Id);
        sourceStock.QuantityReserved = 0;
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.DispatchTransferAsync(transfer.Id, seed.Requester.Id));
    }

    [Fact]
    public async Task DispatchTransferAsync_WhenProductIsMissing_UpdatesExistingDestinationStockWithoutAlerts()
    {
        var seed = await SeedWarehouseTransferAsync(includeDestinationStock: true, destinationManager: false);
        var transfer = await _service.CreateTransferAsync(new TransferCreateDto
        {
            FromWarehouseId = seed.FromWarehouse.Id,
            ToWarehouseId = seed.ToWarehouse.Id,
            RequestedBy = seed.Requester.Id,
            Items =
            [
                new TransferItemDto { ProductId = seed.Product.Id, FromBinId = seed.FromBin.Id, ToBinId = seed.ToBin.Id, QuantityRequested = 2 }
            ]
        });

        await _service.ApproveTransferAsync(transfer.Id, new TransferApprovalDto { ApprovedBy = seed.Requester.Id, Approve = true });

        var missingProductId = Guid.NewGuid();
        var transferItem = await _context.TransferItems.SingleAsync(i => i.TransferId == transfer.Id);
        transferItem.ProductId = missingProductId;

        var sourceStock = await _context.StockLevels.SingleAsync(sl => sl.ProductId == seed.Product.Id && sl.WarehouseId == seed.FromWarehouse.Id && sl.BinLocationId == seed.FromBin.Id);
        sourceStock.ProductId = missingProductId;

        var destinationStock = await _context.StockLevels.SingleAsync(sl => sl.ProductId == seed.Product.Id && sl.WarehouseId == seed.ToWarehouse.Id && sl.BinLocationId == seed.ToBin.Id);
        destinationStock.ProductId = missingProductId;
        await _context.SaveChangesAsync();

        var dispatched = await _service.DispatchTransferAsync(transfer.Id, seed.Requester.Id);

        Assert.Equal(TransferStatus.InTransit, dispatched.Status);
        var updatedDestinationStock = await _context.StockLevels.SingleAsync(sl => sl.ProductId == missingProductId && sl.WarehouseId == seed.ToWarehouse.Id && sl.BinLocationId == seed.ToBin.Id);
        Assert.Equal(0, updatedDestinationStock.QuantityOnHand);
        Assert.Equal(2, updatedDestinationStock.QuantityInTransit);
        _notificationMock.Verify(n => n.SendNotificationAsync(It.IsAny<Guid>(), NotificationChannel.InApp, "TransferDispatched", It.IsAny<string>(), It.IsAny<string>(), "WarehouseTransfer", transfer.Id), Times.Never);
    }

    [Fact]
    public async Task GetTransferByIdAsync_ThrowsWhenCurrentWarehouseDoesNotMatch()
    {
        var seed = await SeedWarehouseTransferAsync();
        var transfer = await _service.CreateTransferAsync(new TransferCreateDto
        {
            FromWarehouseId = seed.FromWarehouse.Id,
            ToWarehouseId = seed.ToWarehouse.Id,
            RequestedBy = seed.Requester.Id,
            Items =
            [
                new TransferItemDto { ProductId = seed.Product.Id, FromBinId = seed.FromBin.Id, ToBinId = seed.ToBin.Id, QuantityRequested = 1 }
            ]
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetTransferByIdAsync(transfer.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetTransfersAsync_RespectsFiltersSearchAndAscendingSort()
    {
        var seedA = await SeedWarehouseTransferAsync();
        var transferA = await _service.CreateTransferAsync(new TransferCreateDto
        {
            FromWarehouseId = seedA.FromWarehouse.Id,
            ToWarehouseId = seedA.ToWarehouse.Id,
            RequestedBy = seedA.Requester.Id,
            Notes = "alpha inbound",
            Items = [new TransferItemDto { ProductId = seedA.Product.Id, FromBinId = seedA.FromBin.Id, ToBinId = seedA.ToBin.Id, QuantityRequested = 1 }]
        });

        var seedB = await SeedWarehouseTransferAsync();
        var transferB = await _service.CreateTransferAsync(new TransferCreateDto
        {
            FromWarehouseId = seedB.FromWarehouse.Id,
            ToWarehouseId = seedB.ToWarehouse.Id,
            RequestedBy = seedB.Requester.Id,
            Notes = "beta outbound",
            Items = [new TransferItemDto { ProductId = seedB.Product.Id, FromBinId = seedB.FromBin.Id, ToBinId = seedB.ToBin.Id, QuantityRequested = 1 }]
        });

        var result = await _service.GetTransfersAsync(new TransferQueryParameters
        {
            FromWarehouseId = seedA.FromWarehouse.Id,
            ToWarehouseId = seedA.ToWarehouse.Id,
            WarehouseId = seedA.FromWarehouse.Id,
            Status = TransferStatus.Requested,
            Search = "alpha",
            SortDir = "asc",
            Page = 1,
            PageSize = 10
        });

        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Data);
        Assert.Equal(transferA.Id, item.Id);
        Assert.Equal("alpha inbound", item.Notes);
    }

    [Fact]
    public async Task GetTransfersAsync_WithNoFilters_UsesDescendingSortAndReturnsAllRows()
    {
        var seedA = await SeedWarehouseTransferAsync();
        var seedB = await SeedWarehouseTransferAsync();

        var transferA = await _service.CreateTransferAsync(new TransferCreateDto
        {
            FromWarehouseId = seedA.FromWarehouse.Id,
            ToWarehouseId = seedA.ToWarehouse.Id,
            RequestedBy = seedA.Requester.Id,
            Notes = "first transfer",
            Items = [new TransferItemDto { ProductId = seedA.Product.Id, FromBinId = seedA.FromBin.Id, ToBinId = seedA.ToBin.Id, QuantityRequested = 1 }]
        });

        await Task.Delay(5);

        var transferB = await _service.CreateTransferAsync(new TransferCreateDto
        {
            FromWarehouseId = seedB.FromWarehouse.Id,
            ToWarehouseId = seedB.ToWarehouse.Id,
            RequestedBy = seedB.Requester.Id,
            Notes = "second transfer",
            Items = [new TransferItemDto { ProductId = seedB.Product.Id, FromBinId = seedB.FromBin.Id, ToBinId = seedB.ToBin.Id, QuantityRequested = 1 }]
        });

        var result = await _service.GetTransfersAsync(new TransferQueryParameters
        {
            SortDir = "desc",
            Page = 1,
            PageSize = 10
        });

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(transferB.Id, result.Data.First().Id);
    }

    [Fact]
    public async Task SearchTransfersAsync_ReturnsProjectedResults()
    {
        var seed = await SeedWarehouseTransferAsync();
        var created = await _service.CreateTransferAsync(new TransferCreateDto
        {
            FromWarehouseId = seed.FromWarehouse.Id,
            ToWarehouseId = seed.ToWarehouse.Id,
            RequestedBy = seed.Requester.Id,
            Notes = "search token",
            Items = [new TransferItemDto { ProductId = seed.Product.Id, FromBinId = seed.FromBin.Id, ToBinId = seed.ToBin.Id, QuantityRequested = 1 }]
        });

        var result = await _service.SearchTransfersAsync(new DynamicQueryRequest
        {
            Page = 1,
            PageSize = 10,
            GlobalSearch = "search"
        });

        Assert.True(result.TotalCount >= 1);
        Assert.Contains(result.Data, t => t.Id == created.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ReceiveTransferAsync_ValidationFailures_ThrowExpectedExceptions(int scenario)
    {
        switch ((ReceiveTransferValidationScenario)scenario)
        {
            case ReceiveTransferValidationScenario.TransferNotFound:
                await Assert.ThrowsAsync<NotFoundException>(() => _service.ReceiveTransferAsync(Guid.NewGuid(), new TransferReceiveDto(), Guid.NewGuid()));
                break;

            case ReceiveTransferValidationScenario.WrongState:
            {
                var seed = await SeedWarehouseTransferAsync();
                var transfer = new WarehouseTransfer
                {
                    Id = Guid.NewGuid(),
                    TransferNumber = "TRF-RCV-WRONG",
                    FromWarehouseId = seed.FromWarehouse.Id,
                    ToWarehouseId = seed.ToWarehouse.Id,
                    RequestedBy = seed.Requester.Id,
                    Status = TransferStatus.Approved,
                    CreatedAt = DateTime.UtcNow,
                    TransferDate = DateTime.UtcNow
                };
                transfer.Items.Add(new TransferItem
                {
                    Id = Guid.NewGuid(),
                    TransferId = transfer.Id,
                    ProductId = seed.Product.Id,
                    FromBinId = seed.FromBin.Id,
                    ToBinId = seed.ToBin.Id,
                    QuantityRequested = 1,
                    QuantityDispatched = 1,
                    QuantityReceived = 0,
                    Product = seed.Product
                });
                await _context.WarehouseTransfers.AddAsync(transfer);
                await _context.SaveChangesAsync();

                await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ReceiveTransferAsync(transfer.Id, new TransferReceiveDto(), seed.Requester.Id));
                break;
            }

            case ReceiveTransferValidationScenario.InvalidItemIds:
            {
                var seed = await SeedWarehouseTransferAsync();
                var transfer = await SeedInTransitTransferAsync(seed, includeVariance: false);
                await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ReceiveTransferAsync(transfer.Id, new TransferReceiveDto
                {
                    Items = [new TransferReceiveItemDto { TransferItemId = Guid.NewGuid(), QuantityReceived = 1 }]
                }, seed.Requester.Id));
                break;
            }

            case ReceiveTransferValidationScenario.NegativeQuantity:
            {
                var seed = await SeedWarehouseTransferAsync();
                var transfer = await SeedInTransitTransferAsync(seed, includeVariance: false);
                var itemId = transfer.Items.First().Id;
                await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ReceiveTransferAsync(transfer.Id, new TransferReceiveDto
                {
                    Items = [new TransferReceiveItemDto { TransferItemId = itemId, QuantityReceived = -1 }]
                }, seed.Requester.Id));
                break;
            }

            case ReceiveTransferValidationScenario.ExceedsDispatched:
            {
                var seed = await SeedWarehouseTransferAsync();
                var transfer = await SeedInTransitTransferAsync(seed, includeVariance: false);
                var itemId = transfer.Items.First().Id;
                await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ReceiveTransferAsync(transfer.Id, new TransferReceiveDto
                {
                    Items = [new TransferReceiveItemDto { TransferItemId = itemId, QuantityReceived = 2 }]
                }, seed.Requester.Id));
                break;
            }
        }
    }

    [Fact]
    public async Task ReceiveTransferAsync_WhenCapacityIsExceeded_ThrowsBusinessRuleException()
    {
        var seed = await SeedWarehouseTransferAsync(destinationManager: false, toBinMaxVolume: 20m);
        var transfer = await SeedInTransitTransferAsync(seed, includeVariance: false);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ReceiveTransferAsync(transfer.Id, new TransferReceiveDto
        {
            Items = [new TransferReceiveItemDto { TransferItemId = transfer.Items.First().Id, QuantityReceived = 1 }]
        }, seed.Requester.Id));
    }

    [Fact]
    public async Task ReceiveTransferAsync_WhenExistingDestinationStockHasInsufficientTransit_ThrowsBusinessRuleException()
    {
        var seed = await SeedWarehouseTransferAsync(includeDestinationStock: true, destinationManager: false);
        var transfer = await SeedInTransitTransferAsync(seed, includeVariance: false);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ReceiveTransferAsync(transfer.Id, new TransferReceiveDto
        {
            Items = [new TransferReceiveItemDto { TransferItemId = transfer.Items.First().Id, QuantityReceived = 1 }]
        }, seed.Requester.Id));
    }

    [Fact]
    public async Task ReceiveTransferAsync_WhenProductIsMissing_SkipsCapacityChecksAndCompletes()
    {
        var seed = await SeedWarehouseTransferAsync(destinationManager: false);
        var transfer = await SeedInTransitTransferAsync(seed, includeVariance: false);

        var missingProductId = Guid.NewGuid();
        var transferItem = transfer.Items.First();
        transferItem.ProductId = missingProductId;

        var sourceStock = await _context.StockLevels.SingleAsync(sl => sl.ProductId == seed.Product.Id && sl.WarehouseId == seed.FromWarehouse.Id && sl.BinLocationId == seed.FromBin.Id);
        sourceStock.ProductId = missingProductId;
        await _context.SaveChangesAsync();

        var result = await _service.ReceiveTransferAsync(transfer.Id, new TransferReceiveDto
        {
            Items = [new TransferReceiveItemDto { TransferItemId = transferItem.Id, QuantityReceived = 1 }]
        }, seed.Requester.Id);

        Assert.Equal(TransferStatus.Received, result.Status);
        var destinationStock = await _context.StockLevels.SingleAsync(sl => sl.ProductId == missingProductId && sl.WarehouseId == seed.ToWarehouse.Id && sl.BinLocationId == seed.ToBin.Id);
        Assert.Equal(1, destinationStock.QuantityOnHand);
    }

    [Fact]
    public async Task ReceiveTransferAsync_WhenSomeItemsAreOmitted_DefaultsMissingQuantitiesToZero()
    {
        var seed = await SeedWarehouseTransferAsync(destinationManager: false);
        var transfer = await SeedInTransitTransferAsync(seed, includeVariance: true);

        var result = await _service.ReceiveTransferAsync(transfer.Id, new TransferReceiveDto
        {
            Items = [new TransferReceiveItemDto { TransferItemId = transfer.Items.First().Id, QuantityReceived = 1 }]
        }, seed.Requester.Id);

        Assert.Equal(TransferStatus.ReceivedWithVariance, result.Status);
        Assert.Equal(2, result.PendingVarianceCount);
        Assert.Equal(1, result.TotalVarianceQuantity);
    }

    [Fact]
    public async Task ReceiveTransferAsync_WhenTargetBinIsMissing_SkipsCapacityChecksAndCreatesDestinationStock()
    {
        var seed = await SeedWarehouseTransferAsync(destinationManager: false);
        var transfer = await SeedInTransitTransferAsync(seed, includeVariance: false);

        var missingBinId = Guid.NewGuid();
        var item = transfer.Items.First();
        item.ToBinId = missingBinId;
        await _context.SaveChangesAsync();

        var result = await _service.ReceiveTransferAsync(transfer.Id, new TransferReceiveDto
        {
            Items = [new TransferReceiveItemDto { TransferItemId = item.Id, QuantityReceived = 1 }]
        }, seed.Requester.Id);

        Assert.Equal(TransferStatus.Received, result.Status);
        var destinationStock = await _context.StockLevels.SingleAsync(sl => sl.ProductId == seed.Product.Id && sl.WarehouseId == seed.ToWarehouse.Id && sl.BinLocationId == missingBinId);
        Assert.Equal(1, destinationStock.QuantityOnHand);
    }

    [Fact]
    public async Task ReceiveTransferAsync_WhenZeroCapacityConfigured_SkipsBinCapacityCheck()
    {
        var seed = await SeedWarehouseTransferAsync(destinationManager: false);
        seed.ToBin.MaxVolumeCm3 = 0m;
        await _context.SaveChangesAsync();
        var transfer = await SeedInTransitTransferAsync(seed, includeVariance: false);

        var result = await _service.ReceiveTransferAsync(transfer.Id, new TransferReceiveDto
        {
            Items = [new TransferReceiveItemDto { TransferItemId = transfer.Items.First().Id, QuantityReceived = 1 }]
        }, seed.Requester.Id);

        Assert.Equal(TransferStatus.Received, result.Status);
    }

    [Fact]
    public async Task ReceiveTransferAsync_WhenWarningsAreBypassed_UsesDefaultOverrideReason()
    {
        var seed = await SeedWarehouseTransferAsync(destinationManager: false, toZoneType: ZoneType.Receiving);
        _currentUserServiceMock.Setup(c => c.Principal).Returns(new ClaimsPrincipal(new ClaimsIdentity("test")));
        _authorizationMock.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());
        var transfer = await SeedInTransitTransferAsync(seed, includeVariance: false);

        var result = await _service.ReceiveTransferAsync(transfer.Id, new TransferReceiveDto
        {
            BypassWarnings = true,
            Items = [new TransferReceiveItemDto { TransferItemId = transfer.Items.First().Id, QuantityReceived = 1 }]
        }, seed.Requester.Id);

        Assert.Equal(TransferStatus.Received, result.Status);
        _publisherMock.Verify(p => p.Publish(
            It.Is<CapacityOverridePerformedEvent>(e =>
                e.RuleBroken == "ZoneMismatch" &&
                e.OverrideReason == "Manual Override"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceiveTransferAsync_WithoutVariance_UpdatesExistingAndNewDestinationStocks()
    {
        var seed = await SeedWarehouseTransferAsync(destinationManager: true);
        var extraCategory = new Category { Id = Guid.NewGuid(), Name = "Receive-Extra", Description = "Tools" };
        var extraProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Receive Widget",
            SKU = "RCV-002",
            CostPrice = 8m,
            SellingPrice = 12m,
            ReorderPoint = 5,
            SafetyStockQty = 2,
            IsActive = true,
            CategoryId = extraCategory.Id,
            Category = extraCategory,
            Length = 1m,
            Width = 1m,
            Height = 1m,
            WeightKg = 1m,
            PreferredBinType = BinType.Standard
        };
        var extraZone = new WarehouseZone
        {
            Id = Guid.NewGuid(),
            Name = "Receive Zone",
            Code = "RCV-Z1",
            ZoneType = ZoneType.Storage,
            WarehouseId = seed.ToWarehouse.Id,
            AreaSqFt = 10,
            MaxVolumeCm3 = 1000,
            MaxWeightKg = 1000
        };
        var extraToBin = new BinLocation
        {
            Id = Guid.NewGuid(),
            BinCode = "RCV-TO-2",
            ZoneId = extraZone.Id,
            Zone = extraZone,
            BinType = BinType.Standard,
            MaxVolumeCm3 = 1000,
            MaxWeightKg = 1000
        };
        var existingDestStock = new StockLevel
        {
            Id = Guid.NewGuid(),
            ProductId = seed.Product.Id,
            WarehouseId = seed.ToWarehouse.Id,
            BinLocationId = seed.ToBin.Id,
            QuantityOnHand = 0,
            QuantityReserved = 0,
            QuantityOnOrder = 0,
            QuantityInTransit = 3,
            LastUpdated = DateTime.UtcNow
        };
        await _context.Categories.AddAsync(extraCategory);
        await _context.Products.AddAsync(extraProduct);
        await _context.WarehouseZones.AddAsync(extraZone);
        await _context.BinLocations.AddAsync(extraToBin);
        await _context.StockLevels.AddAsync(existingDestStock);
        await _context.SaveChangesAsync();

        var transfer = new WarehouseTransfer
        {
            Id = Guid.NewGuid(),
            TransferNumber = "TRF-RCV-NOVAR",
            FromWarehouseId = seed.FromWarehouse.Id,
            ToWarehouseId = seed.ToWarehouse.Id,
            RequestedBy = seed.Requester.Id,
            Status = TransferStatus.InTransit,
            TransferDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        transfer.Items.Add(new TransferItem
        {
            Id = Guid.NewGuid(),
            TransferId = transfer.Id,
            ProductId = seed.Product.Id,
            Product = seed.Product,
            FromBinId = seed.FromBin.Id,
            ToBinId = seed.ToBin.Id,
            QuantityRequested = 3,
            QuantityDispatched = 3,
            QuantityReceived = 0
        });
        transfer.Items.Add(new TransferItem
        {
            Id = Guid.NewGuid(),
            TransferId = transfer.Id,
            ProductId = extraProduct.Id,
            Product = extraProduct,
            FromBinId = null,
            ToBinId = extraToBin.Id,
            QuantityRequested = 2,
            QuantityDispatched = 2,
            QuantityReceived = 0
        });
        await _context.WarehouseTransfers.AddAsync(transfer);
        await _context.SaveChangesAsync();

        var result = await _service.ReceiveTransferAsync(transfer.Id, new TransferReceiveDto
        {
            Items =
            [
                new TransferReceiveItemDto { TransferItemId = transfer.Items.First().Id, QuantityReceived = 3 },
                new TransferReceiveItemDto { TransferItemId = transfer.Items.Last().Id, QuantityReceived = 2 }
            ]
        }, seed.Requester.Id);

        Assert.Equal(TransferStatus.Received, result.Status);
        Assert.Equal(0, result.PendingVarianceCount);
        Assert.Equal(0, result.TotalVarianceQuantity);
        Assert.Equal(0m, result.TotalEstimatedLossValue);

        var stock1 = await _context.StockLevels.SingleAsync(sl => sl.ProductId == seed.Product.Id && sl.WarehouseId == seed.ToWarehouse.Id && sl.BinLocationId == seed.ToBin.Id);
        var stock2 = await _context.StockLevels.SingleAsync(sl => sl.ProductId == extraProduct.Id && sl.WarehouseId == seed.ToWarehouse.Id && sl.BinLocationId == extraToBin.Id);
        Assert.Equal(3, stock1.QuantityOnHand);
        Assert.Equal(0, stock1.QuantityInTransit);
        Assert.Equal(2, stock2.QuantityOnHand);
        Assert.Equal(0, stock2.QuantityInTransit);

        _notificationMock.Verify(n => n.SendNotificationAsync(seed.Requester.Id, NotificationChannel.InApp, "TransferReceived", "Transfer Received Successfully", It.IsAny<string>(), "WarehouseTransfer", transfer.Id), Times.Once);
        Assert.All(result.Items, item => Assert.Null(item.VarianceAdjustmentId));
    }

    [Fact]
    public async Task ReceiveTransferAsync_WithVariance_OverridesWarningsAndCreatesAdjustments()
    {
        var seed = await SeedWarehouseTransferAsync(destinationManager: true);
        _currentUserServiceMock.Setup(c => c.Principal).Returns(new ClaimsPrincipal(new ClaimsIdentity("test")));
        _authorizationMock.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var extraCategory = new Category { Id = Guid.NewGuid(), Name = "Receive-Variance", Description = "Tools" };
        var extraProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Variance Widget",
            SKU = "RCV-003",
            CostPrice = 10m,
            SellingPrice = 12m,
            ReorderPoint = 5,
            SafetyStockQty = 2,
            IsActive = true,
            CategoryId = extraCategory.Id,
            Category = extraCategory,
            Length = 1m,
            Width = 1m,
            Height = 1m,
            WeightKg = 1m,
            PreferredBinType = BinType.Standard
        };
        var extraZone = new WarehouseZone
        {
            Id = Guid.NewGuid(),
            Name = "Variance Zone",
            Code = "VAR-Z1",
            ZoneType = ZoneType.Receiving,
            WarehouseId = seed.ToWarehouse.Id,
            AreaSqFt = 10,
            MaxVolumeCm3 = 1000,
            MaxWeightKg = 1000
        };
        var extraToBin = new BinLocation
        {
            Id = Guid.NewGuid(),
            BinCode = "VAR-TO-2",
            ZoneId = extraZone.Id,
            Zone = extraZone,
            BinType = BinType.Standard,
            MaxVolumeCm3 = 100,
            MaxWeightKg = 1000,
            UtilizedVolumeCm3 = 88m,
            UtilizedWeightKg = 0m
        };
        var existingDestStock = new StockLevel
        {
            Id = Guid.NewGuid(),
            ProductId = seed.Product.Id,
            WarehouseId = seed.ToWarehouse.Id,
            BinLocationId = seed.ToBin.Id,
            QuantityOnHand = 0,
            QuantityReserved = 0,
            QuantityOnOrder = 0,
            QuantityInTransit = 4,
            LastUpdated = DateTime.UtcNow
        };
        await _context.Categories.AddAsync(extraCategory);
        await _context.Products.AddAsync(extraProduct);
        await _context.WarehouseZones.AddAsync(extraZone);
        await _context.BinLocations.AddAsync(extraToBin);
        await _context.StockLevels.AddAsync(existingDestStock);
        await _context.SaveChangesAsync();

        var transfer = new WarehouseTransfer
        {
            Id = Guid.NewGuid(),
            TransferNumber = "TRF-RCV-VAR",
            FromWarehouseId = seed.FromWarehouse.Id,
            ToWarehouseId = seed.ToWarehouse.Id,
            RequestedBy = seed.Requester.Id,
            Status = TransferStatus.InTransit,
            TransferDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        transfer.Items.Add(new TransferItem
        {
            Id = Guid.NewGuid(),
            TransferId = transfer.Id,
            ProductId = seed.Product.Id,
            Product = seed.Product,
            FromBinId = seed.FromBin.Id,
            ToBinId = seed.ToBin.Id,
            QuantityRequested = 4,
            QuantityDispatched = 4,
            QuantityReceived = 0
        });
        transfer.Items.Add(new TransferItem
        {
            Id = Guid.NewGuid(),
            TransferId = transfer.Id,
            ProductId = extraProduct.Id,
            Product = extraProduct,
            FromBinId = null,
            ToBinId = extraToBin.Id,
            QuantityRequested = 5,
            QuantityDispatched = 5,
            QuantityReceived = 0
        });
        transfer.Items.Add(new TransferItem
        {
            Id = Guid.NewGuid(),
            TransferId = transfer.Id,
            ProductId = extraProduct.Id,
            Product = extraProduct,
            FromBinId = null,
            ToBinId = null,
            QuantityRequested = 2,
            QuantityDispatched = 2,
            QuantityReceived = 0
        });
        await _context.WarehouseTransfers.AddAsync(transfer);
        await _context.SaveChangesAsync();

        var result = await _service.ReceiveTransferAsync(transfer.Id, new TransferReceiveDto
        {
            BypassWarnings = true,
            Items =
            [
                new TransferReceiveItemDto { TransferItemId = transfer.Items.First().Id, QuantityReceived = 4 },
                new TransferReceiveItemDto { TransferItemId = transfer.Items.Skip(1).First().Id, QuantityReceived = 3, OverrideReason = "Manual override" },
                new TransferReceiveItemDto { TransferItemId = transfer.Items.Skip(2).First().Id, QuantityReceived = 0 }
            ]
        }, seed.Requester.Id);

        Assert.Equal(TransferStatus.ReceivedWithVariance, result.Status);
        Assert.Equal(TransferVarianceResolutionStatus.PendingApproval, result.VarianceResolutionStatus);
        Assert.Equal(2, result.PendingVarianceCount);
        Assert.Equal(4, result.TotalVarianceQuantity);
        Assert.Equal(40m, result.TotalEstimatedLossValue);
        Assert.Equal(3, result.Items.Count);
        Assert.All(result.Items.Where(i => i.VarianceQuantity > 0), item => Assert.Equal(AdjustmentStatus.Pending, item.VarianceAdjustmentStatus));

        var stock1 = await _context.StockLevels.SingleAsync(sl => sl.ProductId == seed.Product.Id && sl.WarehouseId == seed.ToWarehouse.Id && sl.BinLocationId == seed.ToBin.Id);
        var stock2 = await _context.StockLevels.SingleAsync(sl => sl.ProductId == extraProduct.Id && sl.WarehouseId == seed.ToWarehouse.Id && sl.BinLocationId == extraToBin.Id);
        var stock3 = await _context.StockLevels.SingleAsync(sl => sl.ProductId == extraProduct.Id && sl.WarehouseId == seed.ToWarehouse.Id && sl.BinLocationId == null);
        Assert.Equal(4, stock1.QuantityOnHand);
        Assert.Equal(0, stock1.QuantityInTransit);
        Assert.Equal(3, stock2.QuantityOnHand);
        Assert.Equal(0, stock2.QuantityInTransit);
        Assert.Equal(0, stock3.QuantityOnHand);

        _notificationMock.Verify(n => n.SendNotificationAsync(seed.Requester.Id, NotificationChannel.InApp, "TransferReceivedWithVariance", "Transfer Received with Variance", It.IsAny<string>(), "WarehouseTransfer", transfer.Id), Times.Once);
        _varianceResolverMock.Verify(v => v.NotifyVarianceCreatedAsync(transfer.Id, It.IsAny<Guid>(), It.IsAny<int>(), transfer.TransferNumber, seed.ToWarehouse.Id), Times.Exactly(2));
        _publisherMock.Verify(p => p.Publish(
            It.Is<CapacityOverridePerformedEvent>(e => e.RuleBroken == "ZoneMismatch" && e.ProductId == extraProduct.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        _publisherMock.Verify(p => p.Publish(
            It.Is<BinCapacityThresholdReachedEvent>(e => e.BinId == extraToBin.Id && e.UtilizationPercentage > 90m),
            It.IsAny<CancellationToken>()), Times.Once);

        var fetched = await _service.GetTransferByIdAsync(transfer.Id, seed.FromWarehouse.Id);
        Assert.Equal(2, fetched.PendingVarianceCount);
        Assert.Equal(4, fetched.TotalVarianceQuantity);
        Assert.Equal(40m, fetched.TotalEstimatedLossValue);
        Assert.All(fetched.Items.Where(i => i.VarianceQuantity > 0), item =>
        {
            Assert.Equal("Pending", item.VarianceAdjustmentStatusName);
            Assert.NotNull(item.VarianceAdjustmentId);
        });
    }

    [Fact]
    public async Task ReceiveTransferAsync_WhenCommitThrowsConcurrencyException_ThrowsStaleDataException()
    {
        var seed = await SeedWarehouseTransferAsync();
        var transfer = await SeedInTransitTransferAsync(seed, includeVariance: false);
        var throwingService = new TransferService(
            new ThrowingCommitUnitOfWork(_uow),
            _notificationMock.Object,
            _cacheMock.Object,
            _currentUserServiceMock.Object,
            _publisherMock.Object,
            _authorizationMock.Object,
            _varianceResolverMock.Object);

        await Assert.ThrowsAsync<StaleDataException>(() => throwingService.ReceiveTransferAsync(transfer.Id, new TransferReceiveDto
        {
            Items = [new TransferReceiveItemDto { TransferItemId = transfer.Items.First().Id, QuantityReceived = 1 }]
        }, seed.Requester.Id));
    }

    private enum CreateTransferValidationScenario
    {
        SameWarehouse = 0,
        MissingOriginWarehouse = 1,
        MissingDestinationWarehouse = 2,
        MissingRequester = 3,
        NonPositiveQuantity = 4,
        MissingProduct = 5,
        InsufficientStock = 6
    }

    private enum ApproveTransferValidationScenario
    {
        TransferNotFound = 0,
        WrongState = 1,
        ApproverMissing = 2
    }

    private async Task<SeedData> SeedAsync(bool includeDestinationStock, decimal destinationBinMaxVolume = 1000m)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Hardware", Description = "Tools" };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), Name = "Main Warehouse", Code = "WH-01" };
        var fromZone = new WarehouseZone
        {
            Id = Guid.NewGuid(),
            Name = "Storage A",
            Code = "ST-A",
            ZoneType = ZoneType.Storage,
            WarehouseId = warehouse.Id,
            AreaSqFt = 100,
            MaxVolumeCm3 = 1000,
            MaxWeightKg = 1000
        };
        var toZone = new WarehouseZone
        {
            Id = Guid.NewGuid(),
            Name = "Storage B",
            Code = "ST-B",
            ZoneType = ZoneType.Storage,
            WarehouseId = warehouse.Id,
            AreaSqFt = 100,
            MaxVolumeCm3 = 1000,
            MaxWeightKg = 1000
        };
        var fromBin = new BinLocation
        {
            Id = Guid.NewGuid(),
            BinCode = "FROM-01",
            ZoneId = fromZone.Id,
            BinType = BinType.Standard,
            MaxVolumeCm3 = 1000,
            MaxWeightKg = 1000,
            UtilizedVolumeCm3 = 100m,
            UtilizedWeightKg = 10m
        };
        var toBin = new BinLocation
        {
            Id = Guid.NewGuid(),
            BinCode = "TO-01",
            ZoneId = toZone.Id,
            BinType = BinType.Standard,
            MaxVolumeCm3 = destinationBinMaxVolume,
            MaxWeightKg = 1000
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Widget",
            SKU = "WGT-001",
            CostPrice = 10m,
            SellingPrice = 15m,
            ReorderPoint = 2,
            ReorderQuantity = 5,
            IsActive = true,
            CategoryId = category.Id,
            Category = category,
            Length = 2m,
            Width = 3m,
            Height = 5m,
            WeightKg = 2m,
            PreferredBinType = BinType.Standard
        };
        var sourceStock = new StockLevel
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            BinLocationId = fromBin.Id,
            QuantityOnHand = 10,
            QuantityReserved = 0,
            QuantityOnOrder = 0,
            QuantityInTransit = 0,
            LastUpdated = DateTime.UtcNow
        };

        await _context.Categories.AddAsync(category);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.WarehouseZones.AddAsync(fromZone);
        await _context.WarehouseZones.AddAsync(toZone);
        await _context.BinLocations.AddAsync(fromBin);
        await _context.BinLocations.AddAsync(toBin);
        await _context.Products.AddAsync(product);
        await _context.StockLevels.AddAsync(sourceStock);

        if (includeDestinationStock)
        {
            var destinationStock = new StockLevel
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                BinLocationId = toBin.Id,
                QuantityOnHand = 5,
                QuantityReserved = 0,
                QuantityOnOrder = 0,
                QuantityInTransit = 0,
                LastUpdated = DateTime.UtcNow
            };

            await _context.StockLevels.AddAsync(destinationStock);
        }

        await _context.SaveChangesAsync();

        return new SeedData(
            warehouse,
            product,
            fromBin,
            toBin,
            Guid.NewGuid());
    }

    private sealed record WarehouseTransferSeed(
        Warehouse FromWarehouse,
        Warehouse ToWarehouse,
        User Requester,
        User? DestinationManager,
        Product Product,
        WarehouseZone FromZone,
        WarehouseZone ToZone,
        BinLocation FromBin,
        BinLocation ToBin,
        StockLevel SourceStock,
        StockLevel? DestinationStock);

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

    private async Task<WarehouseTransferSeed> SeedWarehouseTransferAsync(
        bool includeDestinationStock = false,
        bool destinationManager = true,
        int sourceQuantity = 10,
        int sourceReserved = 0,
        int destinationQuantity = 0,
        ZoneType fromZoneType = ZoneType.Storage,
        ZoneType toZoneType = ZoneType.Storage,
        BinType fromBinType = BinType.Standard,
        BinType toBinType = BinType.Standard,
        BinType preferredBinType = BinType.Standard,
        decimal fromBinUtilizedVolume = 100m,
        decimal fromBinUtilizedWeight = 10m,
        decimal toBinUtilizedVolume = 0m,
        decimal toBinMaxVolume = 1000m,
        int safetyStockQty = 2,
        int reorderPoint = 5,
        decimal length = 2m,
        decimal width = 3m,
        decimal height = 5m,
        decimal weight = 2m)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Hardware", Description = "Tools" };
        var fromWarehouse = new Warehouse { Id = Guid.NewGuid(), Name = "Origin WH", Code = "OWH-1" };
        var toWarehouse = new Warehouse { Id = Guid.NewGuid(), Name = "Destination WH", Code = "DWH-1" };
        var fromZone = new WarehouseZone
        {
            Id = Guid.NewGuid(),
            Name = "Origin Zone",
            Code = "OZ-1",
            ZoneType = fromZoneType,
            WarehouseId = fromWarehouse.Id,
            AreaSqFt = 100,
            MaxVolumeCm3 = 10000,
            MaxWeightKg = 10000
        };
        var toZone = new WarehouseZone
        {
            Id = Guid.NewGuid(),
            Name = "Destination Zone",
            Code = "DZ-1",
            ZoneType = toZoneType,
            WarehouseId = toWarehouse.Id,
            AreaSqFt = 100,
            MaxVolumeCm3 = 10000,
            MaxWeightKg = 10000
        };
        var fromBin = new BinLocation
        {
            Id = Guid.NewGuid(),
            BinCode = "FROM-01",
            ZoneId = fromZone.Id,
            Zone = fromZone,
            BinType = fromBinType,
            MaxVolumeCm3 = 1000,
            MaxWeightKg = 1000,
            UtilizedVolumeCm3 = fromBinUtilizedVolume,
            UtilizedWeightKg = fromBinUtilizedWeight
        };
        var toBin = new BinLocation
        {
            Id = Guid.NewGuid(),
            BinCode = "TO-01",
            ZoneId = toZone.Id,
            Zone = toZone,
            BinType = toBinType,
            MaxVolumeCm3 = toBinMaxVolume,
            MaxWeightKg = 1000,
            UtilizedVolumeCm3 = toBinUtilizedVolume,
            UtilizedWeightKg = 0
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Transfer Widget",
            SKU = "TRF-001",
            CostPrice = 10m,
            SellingPrice = 15m,
            ReorderPoint = reorderPoint,
            SafetyStockQty = safetyStockQty,
            IsActive = true,
            CategoryId = category.Id,
            Category = category,
            Length = length,
            Width = width,
            Height = height,
            WeightKg = weight,
            PreferredBinType = preferredBinType
        };
        var role = await _context.Roles.FirstAsync(r => r.Name == "Staff");
        var requester = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Requester",
            Email = "requester@test.com",
            PasswordHash = "hash",
            IsActive = true,
            Status = UserStatus.Active,
            RoleId = role.Id,
            Role = role
        };
        var manager = destinationManager
            ? new User
            {
                Id = Guid.NewGuid(),
                FullName = "Warehouse Manager",
                Email = "manager@test.com",
                PasswordHash = "hash",
                IsActive = true,
                Status = UserStatus.Active,
                RoleId = role.Id,
                Role = role
            }
            : null;

        fromWarehouse.ManagerId = null;
        toWarehouse.ManagerId = manager?.Id;

        var sourceStock = new StockLevel
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = fromWarehouse.Id,
            BinLocationId = fromBin.Id,
            QuantityOnHand = sourceQuantity,
            QuantityReserved = sourceReserved,
            QuantityOnOrder = 0,
            QuantityInTransit = 0,
            LastUpdated = DateTime.UtcNow
        };

        StockLevel? destinationStock = null;
        if (includeDestinationStock)
        {
            destinationStock = new StockLevel
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                WarehouseId = toWarehouse.Id,
                BinLocationId = toBin.Id,
                QuantityOnHand = destinationQuantity,
                QuantityReserved = 0,
                QuantityOnOrder = 0,
                QuantityInTransit = 0,
                LastUpdated = DateTime.UtcNow
            };
        }

        await _context.Categories.AddAsync(category);
        await _context.Warehouses.AddAsync(fromWarehouse);
        await _context.Warehouses.AddAsync(toWarehouse);
        await _context.WarehouseZones.AddAsync(fromZone);
        await _context.WarehouseZones.AddAsync(toZone);
        await _context.BinLocations.AddAsync(fromBin);
        await _context.BinLocations.AddAsync(toBin);
        await _context.Products.AddAsync(product);
        await _context.Users.AddAsync(requester);
        if (manager != null)
            await _context.Users.AddAsync(manager);
        await _context.StockLevels.AddAsync(sourceStock);
        if (destinationStock != null)
            await _context.StockLevels.AddAsync(destinationStock);
        await _context.SaveChangesAsync();

        return new WarehouseTransferSeed(
            fromWarehouse,
            toWarehouse,
            requester,
            manager,
            product,
            fromZone,
            toZone,
            fromBin,
            toBin,
            sourceStock,
            destinationStock);
    }

    private async Task<WarehouseTransfer> SeedInTransitTransferAsync(WarehouseTransferSeed seed, bool includeVariance)
    {
        var transfer = new WarehouseTransfer
        {
            Id = Guid.NewGuid(),
            TransferNumber = $"TRF-IN-{Guid.NewGuid():N}".Substring(0, 16),
            FromWarehouseId = seed.FromWarehouse.Id,
            ToWarehouseId = seed.ToWarehouse.Id,
            RequestedBy = seed.Requester.Id,
            Status = TransferStatus.InTransit,
            TransferDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Items = []
        };

        transfer.Items.Add(new TransferItem
        {
            Id = Guid.NewGuid(),
            TransferId = transfer.Id,
            ProductId = seed.Product.Id,
            Product = seed.Product,
            FromBinId = seed.FromBin.Id,
            ToBinId = seed.ToBin.Id,
            QuantityRequested = 1,
            QuantityDispatched = 1,
            QuantityReceived = 0
        });

        if (includeVariance)
        {
            transfer.Items.Add(new TransferItem
            {
                Id = Guid.NewGuid(),
                TransferId = transfer.Id,
                ProductId = seed.Product.Id,
                Product = seed.Product,
                FromBinId = seed.FromBin.Id,
                ToBinId = null,
                QuantityRequested = 1,
                QuantityDispatched = 1,
                QuantityReceived = 0
            });
        }

        await _context.WarehouseTransfers.AddAsync(transfer);
        await _context.SaveChangesAsync();

        return transfer;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private sealed record SeedData(
        Warehouse Warehouse,
        Product Product,
        BinLocation FromBin,
        BinLocation ToBin,
        Guid PerformedBy);

    private enum ReceiveTransferValidationScenario
    {
        TransferNotFound = 0,
        WrongState = 1,
        InvalidItemIds = 2,
        NegativeQuantity = 3,
        ExceedsDispatched = 4
    }

    private enum DispatchTransferValidationScenario
    {
        TransferNotFound = 0,
        WrongState = 1
    }

    private enum BinTransferValidationScenario
    {
        SameBin = 0,
        NonPositiveQuantity = 1
    }
}
