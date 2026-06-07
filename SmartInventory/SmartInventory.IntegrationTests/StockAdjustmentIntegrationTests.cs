using System;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;
using SmartInventory.Repository;
using SmartInventory.Service.Services;
using Xunit;

namespace SmartInventory.IntegrationTests;

public class StockAdjustmentIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly AppDbContext _dbContext;
    private readonly StockAdjustmentService _service;
    private readonly Guid _userId;

    public StockAdjustmentIntegrationTests(CustomWebApplicationFactory factory)
    {
        var scope = factory.Services.CreateScope();
        _dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(x => x.UserId).Returns(Guid.Parse("b0d33b91-4567-4eef-b123-888888888801"));
        
        var authMock = new Mock<IAuthorizationService>();
        authMock.Setup(x => x.AuthorizeAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<string>())).ReturnsAsync(AuthorizationResult.Success());
        
        var notifMock = new Mock<INotificationService>();
        var cacheMock = new Mock<ICacheService>();
        var pubMock = new Mock<IPublisher>();

        _service = new StockAdjustmentService(
            uow,
            notifMock.Object,
            new NullLogger<StockAdjustmentService>(),
            currentUserMock.Object,
            cacheMock.Object,
            pubMock.Object,
            authMock.Object,
            scope.ServiceProvider.GetRequiredService<ITransferVarianceResolver>()
        );

        _userId = Guid.NewGuid();
        if (!_dbContext.Users.Any(u => u.Id == _userId))
        {
            _dbContext.Users.Add(new User { Id = _userId, FullName = "Tester", Email = $"tester_{Guid.NewGuid()}@test.com", RoleId = _dbContext.Roles.First().Id, PasswordHash = "x", Status = UserStatus.Active });
            _dbContext.SaveChanges();
        }
    }

    [Fact]
    public async Task ApproveAdjustment_Creates_Exactly_One_StockMovement()
    {
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var binId = Guid.NewGuid();

        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ-TEST-1",
            ProductId = productId,
            WarehouseId = warehouseId,
            BinLocationId = binId,
            QuantityBefore = 10,
            QuantityAfter = 15,
            Reason = AdjustmentReason.Found,
            Status = AdjustmentStatus.Pending,
            PerformedBy = _userId,
            CreatedAt = DateTime.UtcNow
        };
        
        _dbContext.StockAdjustments.Add(adjustment);
        await _dbContext.SaveChangesAsync();

        var approvalDto = new StockAdjustmentApprovalDto { Approve = true };
        await _service.ApproveAdjustmentAsync(adjustment.Id, approvalDto);

        var movements = await _dbContext.StockMovements
            .Where(m => m.ReferenceId == adjustment.Id && m.ReferenceType == ReferenceType.Adjustment)
            .ToListAsync();
            
        Assert.Single(movements);
        Assert.Equal(5, movements[0].Quantity);
        Assert.Equal(MovementType.Adjustment, movements[0].MovementType);

        var levels = await _dbContext.StockLevels
            .Where(s => s.ProductId == productId && s.WarehouseId == warehouseId && s.BinLocationId == binId)
            .ToListAsync();
            
        Assert.Single(levels);
        Assert.Equal(15, levels[0].QuantityOnHand);
        
        var outboxMsg = await _dbContext.OutboxMessages
            .Where(m => m.EventType == "StockLevelChanged" && m.Payload.Contains(productId.ToString()))
            .FirstOrDefaultAsync();
            
        Assert.NotNull(outboxMsg);
    }
}
