using MediatR;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Events;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Service.Handlers;

/// <summary>
/// Handles Purchase Order approval/rejection events asynchronously.
/// This cleanly decouples the notification logic from the main database transaction.
/// </summary>
public class PurchaseOrderNotificationHandler : 
    INotificationHandler<PurchaseOrderApprovedEvent>, 
    INotificationHandler<PurchaseOrderRejectedEvent>
{
    private readonly INotificationService _notificationService;

    public PurchaseOrderNotificationHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task Handle(PurchaseOrderApprovedEvent notification, CancellationToken cancellationToken)
    {
        await _notificationService.SendNotificationAsync(
            notification.CreatedBy, 
            NotificationChannel.InApp, 
            "POApproved", 
            "Purchase Order Approved", 
            $"Your Purchase Order {notification.PoNumber} has been approved by {notification.ApproverName}.",
            "PurchaseOrder", 
            notification.PurchaseOrderId);
    }

    public async Task Handle(PurchaseOrderRejectedEvent notification, CancellationToken cancellationToken)
    {
        await _notificationService.SendNotificationAsync(
            notification.CreatedBy, 
            NotificationChannel.InApp, 
            "PORejected", 
            "Purchase Order Rejected", 
            $"Your Purchase Order {notification.PoNumber} has been rejected by {notification.ApproverName}.",
            "PurchaseOrder", 
            notification.PurchaseOrderId);
    }
}
