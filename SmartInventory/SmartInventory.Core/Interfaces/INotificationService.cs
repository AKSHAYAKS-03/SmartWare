using SmartInventory.Core.DTOs;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Interfaces;

public interface INotificationService
{
    /// <summary>
    /// Dispatches a notification to a specific user via the selected channel.
    /// </summary>
    Task SendNotificationAsync(
        Guid userId,
        NotificationChannel channel,
        string eventType,
        string title,
        string message,
        string? entityType = null,
        Guid? entityId = null);

    /// <summary>
    /// Triggered when physical stock drops below or equals the reorder threshold.
    /// </summary>
    Task SendLowStockAlertAsync(Guid productId, Guid warehouseId, int currentStock, int reorderPoint);
    Task SendSafetyStockAlertAsync(Guid productId, Guid warehouseId, int currentStock, int safetyStock);
    Task SendBinCapacityAlertAsync(Guid binId, string binCode, decimal utilizationPercentage);
    
    /// <summary>
    /// Triggered when a PO's expected delivery date has passed without a GRN being created.
    /// </summary>
    Task SendPOOverdueAlertAsync(Guid purchaseOrderId);

    /// <summary>
    /// Returns the notification inbox for a specific user (paginated).
    /// </summary>
    Task<PagedResult<NotificationResponseDto>> GetUserNotificationsAsync(Guid userId, QueryParameters queryParams);

    /// <summary>
    /// Returns the count of unread notifications for a user.
    /// </summary>
    Task<int> GetUnreadCountAsync(Guid userId);

    /// <summary>
    /// Marks a single notification as read for the specified user.
    /// </summary>
    Task MarkAsReadAsync(Guid notificationId, Guid userId);

    /// <summary>
    /// Marks all notifications as read for the specified user.
    /// </summary>
    Task MarkAllAsReadAsync(Guid userId);

    /// <summary>
    /// Sends a welcome invitation email to a newly provisioned employee.
    /// The email contains a one-time secure link for the employee to set their own password.
    /// </summary>
    Task SendWelcomeInviteAsync(Guid userId, string toEmail, string fullName, string inviteToken);

    // ─── Supplier Onboarding Email Notifications ──────────────────────────────

    /// <summary>
    /// Sends an invitation email to a supplier who was invited by admin.
    /// Contains a link to complete registration.
    /// </summary>
    Task SendSupplierInviteAsync(Guid supplierId, string toEmail, string supplierName, string inviteToken);

    /// <summary>
    /// Sends the OTP email to a self-registered supplier for email verification.
    /// </summary>
    Task SendOtpEmailAsync(Guid contactId, string toEmail, string otp, string contactName);

    /// <summary>
    /// Sends approval email to supplier after admin approves their registration.
    /// </summary>
    Task SendApprovalEmailAsync(Guid supplierId, string toEmail, string supplierName, string supplierCode);

    /// <summary>
    /// Sends rejection email to supplier when admin rejects their registration.
    /// </summary>
    Task SendRejectionEmailAsync(Guid supplierId, string toEmail, string supplierName, string reason);

    /// <summary>
    /// Sends suspension email to supplier when admin suspends their account.
    /// </summary>
    Task SendSuspensionEmailAsync(Guid supplierId, string toEmail, string supplierName, string reason);

    /// <summary>
    /// Sends reactivation email to supplier when admin re-activates their account.
    /// </summary>
    Task SendReactivationEmailAsync(Guid supplierId, string toEmail, string supplierName);

    /// <summary>
    /// Sends password reset request email to supplier contact.
    /// </summary>
    Task SendPasswordResetRequestAsync(Guid contactId, string toEmail, string resetToken, string contactName);

    /// <summary>
    /// Sends password reset success confirmation email to supplier contact.
    /// </summary>
    Task SendPasswordResetSuccessAsync(Guid contactId, string toEmail, string contactName);

    // ─── Supplier Finance Notifications ──────────────────────────────────────

    /// <summary>
    /// Notifies the supplier when a Goods Receipt (GRN) is processed against one of their POs.
    /// Contains accepted/rejected quantity breakdown and the current remaining invoiceable amount.
    /// Called from PurchaseOrderService.ReceiveGoodsAsync after commit.
    /// </summary>
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

    /// <summary>
    /// Notifies the supplier when their invoice fails matching (IsMatch = false).
    /// Contains the exact discrepancy reasons and the correct invoiceable amount.
    /// Called from InvoiceProcessingService.MatchInvoiceAsync when IsMatch = false.
    /// </summary>
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
