using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Infrastructure.BackgroundJobs;

/// <summary>
/// Nightly background service that checks for overdue Purchase Orders. Runs daily (every 24 hours).
/// 
/// Approved+Expected date exists+Expected date passed+Actual delivery missing
/// </summary>
/// 
/// 
public class POOverdueCheckerJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<POOverdueCheckerJob> _logger;

    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    public POOverdueCheckerJob(IServiceScopeFactory scopeFactory, ILogger<POOverdueCheckerJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PO Overdue Checker Job started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForOverduePOsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing PO Overdue Checker Job.");
            }

            // Wait before next run (e.g. 24 hours)
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task CheckForOverduePOsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        _logger.LogInformation("Scanning for overdue Purchase Orders...");

        var today = DateTime.UtcNow.Date;

        // Find POs that are approved but expected delivery is in the past
        var overduePOs = await uow.Repository<PurchaseOrder>()
            .Query()
            .Where(po => po.Status == PurchaseOrderStatus.Approved
                      && po.ExpectedDelivery.HasValue
                      && po.ExpectedDelivery.Value.Date < today
                      && po.ActualDelivery == null)
            .Select(po => po.Id)
            .ToListAsync(stoppingToken);

        foreach (var poId in overduePOs)
        {
            await notificationService.SendPOOverdueAlertAsync(poId);
        }

        _logger.LogInformation("Completed PO Overdue scan. Found and alerted on {Count} overdue POs.", overduePOs.Count);
    }
}



