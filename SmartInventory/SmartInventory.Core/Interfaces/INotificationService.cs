using SmartInventory.Core.DTOs;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Interfaces;

public interface INotificationService
{
    
    //Dispatches a notification to a specific user via the selected channel.
    Task SendNotificationAsync(
        Guid userId,
        NotificationChannel channel,
        string eventType,
        string title,
        string message,
        string? entityType = null,
        Guid? entityId = null);

    
     //Triggered when physical stock drops below or equals the reorder threshold.
    Task SendLowStockAlertAsync(Guid productId, Guid warehouseId, int currentStock, int reorderPoint);
    Task SendSafetyStockAlertAsync(Guid productId, Guid warehouseId, int currentStock, int safetyStock);
    Task SendOutOfStockAlertAsync(Guid productId, Guid warehouseId, int currentStock = 0);
    Task SendBinCapacityAlertAsync(Guid binId, string binCode, decimal utilizationPercentage);
    
    
    // Triggered when a PO's expected delivery date has passed without a GRN being created.
    Task SendPOOverdueAlertAsync(Guid purchaseOrderId);
    Task SendPurchaseOrderSubmittedAlertAsync(Guid purchaseOrderId);
    Task SendSupplierPurchaseOrderResponseAlertAsync(Guid purchaseOrderId, bool accepted, string? supplierReason = null);
    Task SendGoodsReceiptVarianceAlertAsync(Guid purchaseOrderId, Guid goodsReceiptId, int totalAcceptedQty, int totalRejectedQty, string? rejectionReasons, decimal remainingInvoiceableAmount);
    Task SendPendingStockAdjustmentApprovalAlertAsync(Guid stockAdjustmentId);
    Task SendInvoiceUploadedAlertAsync(Guid invoiceId);
    Task SendInvoiceApprovedAlertAsync(Guid invoiceId);
    Task SendInvoiceRejectedAlertAsync(Guid invoiceId, string rejectionReason);
    Task SendInvoicePaymentCompletedAlertAsync(Guid invoiceId);
    Task SendInvoicePaymentFailedAlertAsync(Guid invoiceId, string failureReason);

    
    //Returns the notification inbox for a specific user (paginated).
    
    Task<PagedResult<NotificationResponseDto>> GetUserNotificationsAsync(Guid userId, QueryParameters queryParams);

    
    // Returns the count of unread notifications for a user.
    Task<int> GetUnreadCountAsync(Guid userId);

    
    // Marks a single notification as read for the specified user.
    Task MarkAsReadAsync(Guid notificationId, Guid userId);

    
    //Marks all notifications as read for the specified user.
    Task MarkAllAsReadAsync(Guid userId);

    
    //Sends a welcome invitation email to a newly provisioned employee.
    //The email contains a one-time secure link for the employee to set their own password.
    Task SendWelcomeInviteAsync(Guid userId, string toEmail, string fullName, string inviteToken);


    
    //Sends an invitation email to a supplier who was invited by admin.
    //Contains a link to complete registration.
    Task SendSupplierInviteAsync(Guid supplierId, string toEmail, string supplierName, string inviteToken);

    
    //Sends the OTP email to a self-registered supplier for email verification.
    Task SendOtpEmailAsync(Guid contactId, string toEmail, string otp, string contactName);

    
    //Sends approval email to supplier after admin approves their registration.
    Task SendApprovalEmailAsync(Guid supplierId, string toEmail, string supplierName, string supplierCode);

    
    //Sends rejection email to supplier when admin rejects their registration.
    Task SendRejectionEmailAsync(Guid supplierId, string toEmail, string supplierName, string reason);

    
    //Sends suspension email to supplier when admin suspends their account.
    Task SendSuspensionEmailAsync(Guid supplierId, string toEmail, string supplierName, string reason);

    
    //Sends reactivation email to supplier when admin re-activates their account.
    Task SendReactivationEmailAsync(Guid supplierId, string toEmail, string supplierName);

    
    //Sends password reset request email to supplier contact.
    Task SendPasswordResetRequestAsync(Guid contactId, string toEmail, string resetToken, string contactName);

    
    //Sends password reset success confirmation email to supplier contact.
    Task SendPasswordResetSuccessAsync(Guid contactId, string toEmail, string contactName);


    
    //Notifies the supplier when a Goods Receipt (GRN) is processed against one of their POs.
    //Contains accepted/rejected quantity breakdown and the current remaining invoiceable amount.
    //Called from PurchaseOrderService.ReceiveGoodsAsync after commit.
    Task SendSupplierGoodsReceiptNotificationAsync(
        Guid supplierId,
        string supplierEmail,
        string supplierName,
        string poNumber,
        string grnNumber,
        int totalAcceptedQty,
        int totalRejectedQty,
        string? rejectionReasons,
        decimal aggregateAcceptedGrnValue,
        decimal remainingInvoiceableAmount);

    
    //Notifies the supplier when their invoice fails matching (IsMatch = false).
    //Contains the exact discrepancy reasons and the correct invoiceable amount.
    //Called from InvoiceProcessingService.MatchInvoiceAsync when IsMatch = false.
    Task SendSupplierInvoiceRejectedNotificationAsync(
        Guid supplierId,
        string supplierEmail,
        string supplierName,
        string invoiceNumber,
        string poNumber,
        decimal invoiceAmount,
        decimal aggregateAcceptedGrnValue,
        decimal remainingInvoiceableAmount,
        string discrepancyReason);
}
