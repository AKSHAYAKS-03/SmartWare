using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using StackExchange.Redis;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;
using SmartInventory.Repository;

namespace SmartInventory.Infrastructure.Services;

public class OutboxProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessorService> _logger;
    private readonly string _connectionString;
    private readonly IConnectionMultiplexer? _redis;

    public OutboxProcessorService(
        IServiceProvider serviceProvider, 
        ILogger<OutboxProcessorService> logger, 
        IConfiguration config,
        IConnectionMultiplexer? redis = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        _redis = redis;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Processor Service started.");

        var pollingTask = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessOutboxAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }, stoppingToken);

        var listenTask = ListenForNotificationsAsync(stoppingToken);

        await Task.WhenAny(pollingTask, listenTask);
    }

    private async Task ListenForNotificationsAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(stoppingToken);

                connection.Notification += async (o, e) =>
                {
                    if (e.Channel == "outbox_ready")
                    {
                        try
                        {
                            _logger.LogInformation("Received NOTIFY outbox_ready. Processing outbox...");
                            await ProcessOutboxAsync(stoppingToken);
                        }
                        catch (OperationCanceledException)
                        {
                            // Ignore cancellation
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing outbox from notification.");
                        }
                    }
                };

                using (var cmd = new NpgsqlCommand("LISTEN outbox_ready;", connection))
                {
                    await cmd.ExecuteNonQueryAsync(stoppingToken);
                }

                while (!stoppingToken.IsCancellationRequested)
                {
                    await connection.WaitAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error in LISTEN/NOTIFY loop. Reconnecting in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ProcessOutboxAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var realtimeService = scope.ServiceProvider.GetRequiredService<IRealtimeService>();

        // NpgsqlRetryingExecutionStrategy requires all manual transactions to be
        // wrapped inside CreateExecutionStrategy().ExecuteAsync() to be retriable.
        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);

            var messages = await dbContext.OutboxMessages
                .FromSqlRaw(@"
                    SELECT * FROM outbox_messages 
                    WHERE ""Status"" IN ('Pending', 'Failed') 
                       OR (""Status"" = 'Processing' AND ""ProcessedAt"" IS NULL AND ""CreatedAt"" < NOW() - INTERVAL '5 minutes')
                    ORDER BY ""CreatedAt""
                    LIMIT 100
                    FOR UPDATE SKIP LOCKED")
                .ToListAsync(stoppingToken);

            if (!messages.Any())
            {
                await transaction.RollbackAsync(stoppingToken);
                return;
            }

            foreach (var msg in messages)
            {
                if (msg.RetryCount >= 3)
                {
                    msg.Status = "DeadLetter";
                    continue;
                }

                try
                {
                    // Mark as Processing to lock it
                    msg.Status = "Processing";
                    await dbContext.SaveChangesAsync(stoppingToken);

                    bool processed = false;

                    if (msg.EventType == "StockLevelChanged")
                    {
                        if (_redis != null)
                        {
                            var subscriber = _redis.GetSubscriber();
                            await subscriber.PublishAsync(RedisChannel.Literal("realtime_stock_updates"), msg.Payload);
                        }
                        processed = true;
                    }
                    else if (msg.EventType == "SendNotification")
                    {
                        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                        var payload = JsonSerializer.Deserialize<OutboxNotificationPayload>(msg.Payload);
                        if (payload != null)
                        {
                            await ProcessNotificationMessageAsync(dbContext, realtimeService, payload, emailService);
                        }
                        processed = true;
                    }

                    if (processed)
                    {
                        msg.Status = "Processed";
                        msg.ProcessedAt = DateTime.UtcNow;
                        msg.ErrorMessage = null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing outbox message {Id}", msg.Id);
                    msg.RetryCount++;
                    msg.Status = msg.RetryCount >= 3 ? "DeadLetter" : "Failed";
                    msg.ErrorMessage = ex.Message;
                }
            }

            await dbContext.SaveChangesAsync(stoppingToken);
            await transaction.CommitAsync(stoppingToken);
        });
    }

    private async Task ProcessNotificationMessageAsync(
        AppDbContext dbContext,
        IRealtimeService realtimeService,
        OutboxNotificationPayload payload,
        IEmailService emailService)
    {
        var user = await dbContext.Set<User>().FindAsync(payload.UserId);
        if (user == null)
        {
            throw new Exception($"User with ID {payload.UserId} was not found.");
        }

        var deliveryStatus = NotificationStatus.Sent;
        string? error = null;

        if (payload.Channel == NotificationChannel.Email)
        {
            await emailService.SendEmailAsync(user.Email, payload.Title, payload.Message, isHtml: true);
        }
        else if (payload.Channel == NotificationChannel.SMS)
        {
            if (string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                deliveryStatus = NotificationStatus.Failed;
                error = "User does not have a phone number registered.";
            }
            else
            {
                _logger.LogInformation("OUTBOX SIMULATED SMS REST SEND to {Phone}: {Message}", user.PhoneNumber, payload.Message);
            }
        }
        else if (payload.Channel == NotificationChannel.InApp)
        {
            await realtimeService.SendNotificationToUserAsync(payload.UserId, payload.Title, payload.Message, payload.EventType, payload.EntityId);
        }

        var log = new NotificationLog
        {
            Id = Guid.NewGuid(),
            UserId = payload.UserId,
            Channel = payload.Channel,
            EventType = payload.EventType,
            Recipient = payload.Channel switch
            {
                NotificationChannel.Email => user.Email,
                NotificationChannel.SMS => user.PhoneNumber ?? "Unknown",
                NotificationChannel.InApp => user.FullName,
                _ => "Unknown"
            },
            Status = deliveryStatus,
            ErrorMessage = error,
            SentAt = deliveryStatus == NotificationStatus.Sent ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow
        };

        await dbContext.Set<NotificationLog>().AddAsync(log);
    }
}
