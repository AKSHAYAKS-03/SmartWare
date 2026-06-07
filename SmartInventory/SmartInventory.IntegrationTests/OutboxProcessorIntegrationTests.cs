using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;
using SmartInventory.Infrastructure.Services;
using SmartInventory.Repository;
using Xunit;

namespace SmartInventory.IntegrationTests;

public class OutboxProcessorIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly AppDbContext _dbContext;
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<ISubscriber> _subscriberMock;
    private readonly Mock<IRealtimeService> _realtimeMock;
    private readonly OutboxProcessorService _service;
    private static bool _userSeeded = false;
    private static readonly object _lock = new object();

    public OutboxProcessorIntegrationTests(CustomWebApplicationFactory factory)
    {
        var scope = factory.Services.CreateScope();
        _dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        lock (_lock)
        {
            if (!_userSeeded && !_dbContext.Users.Any(u => u.Id == Guid.Empty))
            {
                try {
                    _dbContext.Users.Add(new User { Id = Guid.Empty, FullName = "System", Email = $"system_{Guid.NewGuid()}@test.com", RoleId = _dbContext.Roles.First().Id, PasswordHash = "x", Status = UserStatus.Active });
                    _dbContext.SaveChanges();
                } catch { }
                _userSeeded = true;
            }
        }
        
        _subscriberMock = new Mock<ISubscriber>();
        _redisMock = new Mock<IConnectionMultiplexer>();
        _redisMock.Setup(r => r.GetSubscriber(It.IsAny<object>())).Returns(_subscriberMock.Object);

        _realtimeMock = new Mock<IRealtimeService>();

        var services = new ServiceCollection();
        services.AddScoped<AppDbContext>(_ => _dbContext);
        services.AddScoped<IRealtimeService>(_ => _realtimeMock.Object);
        var customProvider = services.BuildServiceProvider();

        _service = new OutboxProcessorService(
            customProvider,
            new NullLogger<OutboxProcessorService>(),
            scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(),
            _redisMock.Object);
    }

    private async Task RunProcessOutboxAsync()
    {
        var method = typeof(OutboxProcessorService).GetMethod("ProcessOutboxAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task)method!.Invoke(_service, new object[] { CancellationToken.None })!;
        await task;
    }

    [Fact]
    public async Task Outbox_Processes_SendNotification_Successfully()
    {
        var userId = Guid.NewGuid();
        _dbContext.Users.Add(new User { Id = userId, FullName = "Test", Email = $"test_{Guid.NewGuid()}@test.com", RoleId = _dbContext.Roles.First().Id, PasswordHash = "x", Status = UserStatus.Active });

        var payload = new OutboxNotificationPayload { UserId = userId, Channel = NotificationChannel.InApp, Title = "Test", Message = "Message" };
        var msg = new OutboxMessage { Id = Guid.NewGuid(), EventType = "SendNotification", Payload = JsonSerializer.Serialize(payload), Status = "Pending", CreatedAt = DateTime.UtcNow };
        _dbContext.OutboxMessages.Add(msg);
        await _dbContext.SaveChangesAsync();

        await RunProcessOutboxAsync();

        var updatedMsg = await _dbContext.OutboxMessages.FindAsync(msg.Id);
        Assert.Equal("Processed", updatedMsg!.Status);
        
        _realtimeMock.Verify(r => r.SendNotificationToUserAsync(userId, "Test", "Message", It.IsAny<string>(), It.IsAny<Guid?>()), Times.Once);
    }

    [Fact]
    public async Task Outbox_Processes_StockLevelChanged_Successfully()
    {
        var msg = new OutboxMessage { Id = Guid.NewGuid(), EventType = "StockLevelChanged", Payload = "{\"test\": 1}", Status = "Pending", CreatedAt = DateTime.UtcNow };
        _dbContext.OutboxMessages.Add(msg);
        await _dbContext.SaveChangesAsync();

        await RunProcessOutboxAsync();

        var updatedMsg = await _dbContext.OutboxMessages.FindAsync(msg.Id);
        Assert.Equal("Processed", updatedMsg!.Status);

        _subscriberMock.Verify(s => s.PublishAsync(It.Is<RedisChannel>(c => c.ToString() == "realtime_stock_updates"), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task Outbox_MixedBatch_Processes_All_Without_Swallowing()
    {
        var userId = Guid.NewGuid();
        _dbContext.Users.Add(new User { Id = userId, FullName = "Test2", Email = $"test_{Guid.NewGuid()}@test.com", RoleId = _dbContext.Roles.First().Id, PasswordHash = "x", Status = UserStatus.Active });

        var payload = new OutboxNotificationPayload { UserId = userId, Channel = NotificationChannel.InApp, Title = "Test", Message = "Message" };
        
        var msg1 = new OutboxMessage { Id = Guid.NewGuid(), EventType = "StockLevelChanged", Payload = "{}", Status = "Pending", CreatedAt = DateTime.UtcNow };
        var msg2 = new OutboxMessage { Id = Guid.NewGuid(), EventType = "SendNotification", Payload = JsonSerializer.Serialize(payload), Status = "Pending", CreatedAt = DateTime.UtcNow };
        
        _dbContext.OutboxMessages.AddRange(msg1, msg2);
        await _dbContext.SaveChangesAsync();

        await RunProcessOutboxAsync();

        var updatedMsg1 = await _dbContext.OutboxMessages.FindAsync(msg1.Id);
        var updatedMsg2 = await _dbContext.OutboxMessages.FindAsync(msg2.Id);
        
        Assert.Equal("Processed", updatedMsg1!.Status);
        Assert.Equal("Processed", updatedMsg2!.Status);

        _subscriberMock.Verify(s => s.PublishAsync(It.IsAny<RedisChannel>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Once);
        _realtimeMock.Verify(r => r.SendNotificationToUserAsync(userId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Once);
    }
}
