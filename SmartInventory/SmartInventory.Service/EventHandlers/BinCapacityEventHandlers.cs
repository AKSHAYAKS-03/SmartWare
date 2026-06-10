using MediatR;
using Microsoft.Extensions.Logging;
using SmartInventory.Core.Events;
using SmartInventory.Core.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace SmartInventory.Service.EventHandlers;

public class BinCapacityEventHandlers : 
    INotificationHandler<BinCapacityThresholdReachedEvent>,
    INotificationHandler<CapacityOverridePerformedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<BinCapacityEventHandlers> _logger;

    public BinCapacityEventHandlers(
        INotificationService notificationService,
        ILogger<BinCapacityEventHandlers> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Handle(BinCapacityThresholdReachedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Domain Event Received: BinCapacityThresholdReachedEvent for Bin {BinCode} ({Utilization}%)", 
            notification.BinCode, notification.UtilizationPercentage);

        // Dispatch via existing notification channels (In-App, SMS, Email, etc.)
        await _notificationService.SendBinCapacityAlertAsync(
            notification.BinId, 
            notification.BinCode, 
            notification.UtilizationPercentage);
    }

    public Task Handle(CapacityOverridePerformedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Domain Event Received: CapacityOverridePerformedEvent. User {UserId} overrode {RuleBroken} on Bin {BinCode}. Reason: {Reason}", 
            notification.UserId, notification.RuleBroken, notification.BinCode, notification.OverrideReason);

        // In a real enterprise system, this might dispatch a highly secure email to the Audit/Compliance team
        // or trigger a webhook to a SIEM system. For now, structured logging is sufficient.
        
        return Task.CompletedTask;
    }
}
