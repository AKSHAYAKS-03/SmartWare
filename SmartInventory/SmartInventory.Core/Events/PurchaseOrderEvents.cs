using MediatR;
using System;

namespace SmartInventory.Core.Events;

/// <summary>
/// Event dispatched when a Purchase Order is approved.
/// Handles out-of-band side effects (e.g. notifications) without blocking the HTTP request.
/// </summary>
public class PurchaseOrderApprovedEvent : INotification
{
    public Guid PurchaseOrderId { get; }
    public string PoNumber { get; }
    public Guid CreatedBy { get; }
    public string ApproverName { get; }

    public PurchaseOrderApprovedEvent(Guid purchaseOrderId, string poNumber, Guid createdBy, string approverName)
    {
        PurchaseOrderId = purchaseOrderId;
        PoNumber = poNumber;
        CreatedBy = createdBy;
        ApproverName = approverName;
    }
}

/// <summary>
/// Event dispatched when a Purchase Order is rejected.
/// </summary>
public class PurchaseOrderRejectedEvent : INotification
{
    public Guid PurchaseOrderId { get; }
    public string PoNumber { get; }
    public Guid CreatedBy { get; }
    public string ApproverName { get; }

    public PurchaseOrderRejectedEvent(Guid purchaseOrderId, string poNumber, Guid createdBy, string approverName)
    {
        PurchaseOrderId = purchaseOrderId;
        PoNumber = poNumber;
        CreatedBy = createdBy;
        ApproverName = approverName;
    }
}
