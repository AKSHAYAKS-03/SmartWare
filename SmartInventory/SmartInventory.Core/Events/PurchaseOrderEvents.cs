using MediatR;
using System;

namespace SmartInventory.Core.Events;

// Event dispatched when a Purchase Order is approved.
public record PurchaseOrderApprovedEvent(
    Guid PurchaseOrderId,
    string PoNumber,
    Guid CreatedBy,
    string ApproverName
) : INotification;

// Event dispatched when a Purchase Order is rejected.
public record PurchaseOrderRejectedEvent(
    Guid PurchaseOrderId,
    string PoNumber,
    Guid CreatedBy,
    string ApproverName
) : INotification;