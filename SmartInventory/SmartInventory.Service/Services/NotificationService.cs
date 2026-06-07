using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Service.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _uow;
    private readonly IRealtimeService _realtimeService;
    private readonly ILogger<NotificationService> _logger;
    private readonly IEmailService _emailService;

    public NotificationService(
        IUnitOfWork uow,
        IRealtimeService realtimeService,
        ILogger<NotificationService> logger,
        IEmailService emailService)
    {
        _uow = uow;
        _realtimeService = realtimeService;
        _logger = logger;
        _emailService = emailService;
    }

    public async Task SendNotificationAsync(
        Guid userId,
        NotificationChannel channel,
        string eventType,
        string title,
        string message,
        string? entityType = null,
        Guid? entityId = null)
    {
        var user = await _uow.Repository<User>().GetByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User with ID {UserId} was not found. Cannot send notification.", userId);
            return;
        }

        Guid? notificationId = null;

        // 1. Create a persistent In-App notification record in the database if channel is InApp
        if (channel == NotificationChannel.InApp)
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Channel = channel,
                Type = eventType,
                Title = title,
                Message = message,
                EntityType = entityType,
                EntityId = entityId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            notificationId = notification.Id;
            await _uow.Repository<Notification>().AddAsync(notification);
        }

        // 2. Queue delivery task via Transactional Outbox
        var payload = new OutboxNotificationPayload
        {
            UserId = userId,
            Channel = channel,
            EventType = eventType,
            Title = title,
            Message = message,
            EntityType = entityType,
            EntityId = entityId,
            NotificationId = notificationId
        };

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "SendNotification",
            Payload = System.Text.Json.JsonSerializer.Serialize(payload),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<OutboxMessage>().AddAsync(outboxMessage);
        await _uow.CommitAsync();
    }

    public async Task SendLowStockAlertAsync(Guid productId, Guid warehouseId, int currentStock, int reorderPoint)
    {
        var product = await _uow.Repository<Product>().GetByIdAsync(productId);
        var warehouse = await _uow.Repository<Warehouse>().GetByIdAsync(warehouseId);

        if (product == null || warehouse == null) return;

        // Fetch managers assigned to this warehouse or general admins to notify them
        // For simplicity and business logic compliance, we look at users who have Manager or Admin role
        var users = await _uow.Repository<User>().GetAllAsync();
        
        // Find managers who are assigned as manager of this warehouse, or have warehouse assignment
        var warehouseManagerId = warehouse.ManagerId;
        
        var message = $"Low Stock Alert in '{warehouse.Name}': Product '{product.Name}' (SKU: {product.SKU}) has hit {currentStock} units (Reorder Threshold: {reorderPoint}).";
        var title = "Low Stock Warning";

        if (warehouseManagerId.HasValue)
        {
            // Send In-App & SMS if SMS is enabled on the user
            var manager = users.FirstOrDefault(u => u.Id == warehouseManagerId.Value);
            if (manager != null)
            {
                await SendNotificationAsync(manager.Id, NotificationChannel.InApp, "LowStock", title, message, "Product", productId);
                if (manager.SmsEnabled)
                {
                    await SendNotificationAsync(manager.Id, NotificationChannel.SMS, "LowStock", title, message, "Product", productId);
                }
            }
        }
    }
    public async Task SendSafetyStockAlertAsync(Guid productId, Guid warehouseId, int currentStock, int safetyStock)
    {
        var product = await _uow.Repository<Product>().GetByIdAsync(productId);
        var warehouse = await _uow.Repository<Warehouse>().GetByIdAsync(warehouseId);

        if (product == null || warehouse == null) return;

        var users = await _uow.Repository<User>().GetAllAsync();
        var warehouseManagerId = warehouse.ManagerId;
        
        var message = $"CRITICAL: Safety Stock Alert in '{warehouse.Name}': Product '{product.Name}' (SKU: {product.SKU}) has hit {currentStock} units (Safety Threshold: {safetyStock}). Operations may be at risk.";
        var title = "Safety Stock Critical Alert";

        if (warehouseManagerId.HasValue)
        {
            var manager = users.FirstOrDefault(u => u.Id == warehouseManagerId.Value);
            if (manager != null)
            {
                await SendNotificationAsync(manager.Id, NotificationChannel.InApp, "SafetyStock", title, message, "Product", productId);
                if (manager.SmsEnabled)
                {
                    await SendNotificationAsync(manager.Id, NotificationChannel.SMS, "SafetyStock", title, message, "Product", productId);
                }
            }
        }
    }

    public async Task SendBinCapacityAlertAsync(Guid binId, string binCode, decimal utilizationPercentage)
    {
        var bin = await _uow.Repository<BinLocation>()
            .Query()
            .Include(b => b.Zone)
                .ThenInclude(z => z.Warehouse)
            .FirstOrDefaultAsync(b => b.Id == binId);

        if (bin == null) return;

        var warehouse = bin.Zone?.Warehouse;
        if (warehouse == null) return;

        var users = await _uow.Repository<User>().GetAllAsync();
        var warehouseManagerId = warehouse.ManagerId;
        
        var message = $"Bin Capacity Warning in '{warehouse.Name}': Bin {binCode} is at {utilizationPercentage:F1}% capacity.";
        var title = "Bin Capacity Alert";

        if (warehouseManagerId.HasValue)
        {
            var manager = users.FirstOrDefault(u => u.Id == warehouseManagerId.Value);
            if (manager != null)
            {
                await SendNotificationAsync(manager.Id, NotificationChannel.InApp, "BinCapacity", title, message, "BinLocation", binId);
            }
        }
    }

    public async Task SendPOOverdueAlertAsync(Guid purchaseOrderId)
    {
        var po = await _uow.Repository<PurchaseOrder>()
            .Query()
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.CreatedByUser)
            .FirstOrDefaultAsync(p => p.Id == purchaseOrderId);

        if (po == null) return;

        var users = await _uow.Repository<User>().GetAllAsync();
        var warehouseManagerId = po.Warehouse.ManagerId;
        
        var expectedDate = po.ExpectedDelivery?.ToString("dd-MMM-yyyy") ?? "Unknown";
        var title = "PO Overdue Alert";
        var message = $"Purchase Order {po.PoNumber} from '{po.Supplier.Name}' is overdue. Expected: {expectedDate}. No goods receipt has been recorded.";

        // Notify the creator
        await SendNotificationAsync(po.CreatedBy, NotificationChannel.InApp, "POOverdue", title, message, "PurchaseOrder", po.Id);
        if (po.CreatedByUser?.SmsEnabled == true)
        {
            await SendNotificationAsync(po.CreatedBy, NotificationChannel.SMS, "POOverdue", title, message, "PurchaseOrder", po.Id);
        }

        // Notify the warehouse manager
        if (warehouseManagerId.HasValue && warehouseManagerId.Value != po.CreatedBy)
        {
            var manager = users.FirstOrDefault(u => u.Id == warehouseManagerId.Value);
            if (manager != null)
            {
                await SendNotificationAsync(manager.Id, NotificationChannel.InApp, "POOverdue", title, message, "PurchaseOrder", po.Id);
            }
        }
    }

    public async Task<PagedResult<NotificationResponseDto>> GetUserNotificationsAsync(
        Guid userId, QueryParameters queryParams)
    {
        var query = _uow.Repository<Notification>()
            .Query()
            .Include(n => n.User)
            .Where(n => n.UserId == userId);

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
            query = query.Where(n => n.Title.Contains(queryParams.Search) ||
                                     n.Message.Contains(queryParams.Search));

        int total = await query.CountAsync();

        var data = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync();

        return new PagedResult<NotificationResponseDto>
        {
            Data = data.Select(n => new NotificationResponseDto
            {
                Id = n.Id,
                UserId = n.UserId,
                UserFullName = n.User?.FullName ?? string.Empty,
                Channel = n.Channel,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                EntityType = n.EntityType,
                EntityId = n.EntityId,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            }),
            TotalCount = total,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    public async Task<int> GetUnreadCountAsync(Guid userId) =>
        await _uow.Repository<Notification>()
            .Query()
            .CountAsync(n => n.UserId == userId && !n.IsRead);

    public async Task MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        var notification = await _uow.Repository<Notification>()
            .Query()
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null) return; // Silently ignore — prevent enumeration

        notification.IsRead = true;
        _uow.Repository<Notification>().Update(notification);
        await _uow.CommitAsync();
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        var unread = await _uow.Repository<Notification>()
            .Query()
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread)
        {
            n.IsRead = true;
            _uow.Repository<Notification>().Update(n);
        }

        if (unread.Any()) await _uow.CommitAsync();
    }

    /// <summary>
    /// Sends a welcome/invitation email to a newly provisioned employee.
    /// Contains a one-time secure link for the employee to set their own password.
    /// </summary>
    public async Task SendWelcomeInviteAsync(Guid userId, string toEmail, string fullName, string inviteToken)
    {
        // Construct the invite link (frontend base URL should come from config in production)
        var inviteLink = $"https://app.smartware.com/set-password?token={inviteToken}";

        _logger.LogInformation(
            "[INVITE] Welcome email queued for {Email}. Link: {Link}. Expires in 48 hours.",
            toEmail, inviteLink);

        var htmlBody = $@"
            <h2>Welcome to SmartInventory, {fullName}!</h2>
            <p>Your account has been created. Please click the link below to set your password.</p>
            <p><a href='{inviteLink}'>Set Password</a></p>
            <p>This link will expire in 48 hours.</p>
        ";

        // Dispatch via the Transactional Outbox so the API thread doesn't wait for SMTP
        await SendNotificationAsync(userId, NotificationChannel.Email, "WelcomeInvite", "Welcome to SmartInventory", htmlBody, "User", userId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SUPPLIER ONBOARDING EMAIL NOTIFICATIONS
    // ─────────────────────────────────────────────────────────────────────────

    public async Task SendSupplierInviteAsync(Guid supplierId, string toEmail, string supplierName, string inviteToken)
    {
        var inviteLink = $"https://app.smartware.com/supplier/complete-registration?token={inviteToken}";

        _logger.LogInformation(
            "[SUPPLIER_INVITE] Email queued for {Email}. Link: {Link}. Expires in 7 days.",
            toEmail, inviteLink);

        var htmlBody = $@"
            <h2>Invitation to Join SmartInventory Supplier Portal</h2>
            <p>Dear {supplierName},</p>
            <p>You have been invited to join the SmartInventory Supplier Portal. Please click the link below to complete your registration.</p>
            <p><a href='{inviteLink}'>Complete Registration</a></p>
            <p>This link will expire in 7 days.</p>
            <p>Once registered, your account will be reviewed by our team and you will receive a notification when approved.</p>
        ";

        await SendNotificationAsync(supplierId, NotificationChannel.Email, "SupplierInvite", "Invitation to SmartInventory Supplier Portal", htmlBody, "Supplier", supplierId);
    }

    public async Task SendOtpEmailAsync(Guid contactId, string toEmail, string otp, string contactName)
    {
        _logger.LogInformation(
            "[OTP] OTP email queued for {Email}. OTP: {Otp}. Expires in 15 minutes.",
            toEmail, otp);

        var htmlBody = $@"
            <h2>Verify Your Email - SmartInventory Supplier Portal</h2>
            <p>Dear {contactName},</p>
            <p>Your verification OTP is:</p>
            <h1 style='font-size: 32px; letter-spacing: 5px; text-align: center; margin: 20px 0;'>{otp}</h1>
            <p><strong>This OTP will expire in 15 minutes.</strong></p>
            <p>If you didn't request this, please ignore this email.</p>
        ";

        await SendNotificationAsync(contactId, NotificationChannel.Email, "OtpVerification", "Verify Your Email - SmartInventory", htmlBody, "SupplierContact", contactId);
    }

    public async Task SendApprovalEmailAsync(Guid supplierId, string toEmail, string supplierName, string supplierCode)
    {
        _logger.LogInformation(
            "[APPROVAL] Approval email queued for {Email}. Supplier Code: {Code}.",
            toEmail, supplierCode);

        var htmlBody = $@"
            <h2>Congratulations! Your Supplier Account is Approved</h2>
            <p>Dear {supplierName},</p>
            <p>Great news! Your supplier account has been approved by SmartInventory.</p>
            <p>Your supplier code is: <strong>{supplierCode}</strong></p>
            <p>You can now start transacting with us through the Supplier Portal.</p>
            <p>Thank you for partnering with us!</p>
        ";

        await SendNotificationAsync(supplierId, NotificationChannel.Email, "SupplierApproved", "Your Account is Approved - SmartInventory", htmlBody, "Supplier", supplierId);
    }

    public async Task SendRejectionEmailAsync(Guid supplierId, string toEmail, string supplierName, string reason)
    {
        _logger.LogInformation(
            "[REJECTION] Rejection email queued for {Email}. Reason: {Reason}.",
            toEmail, reason);

        var htmlBody = $@"
            <h2>Supplier Registration - Not Approved</h2>
            <p>Dear {supplierName},</p>
            <p>Thank you for your interest in becoming a SmartInventory supplier.</p>
            <p>After review, we are unable to approve your application at this time.</p>
            <p><strong>Reason:</strong> {reason}</p>
            <p>If you have questions, please contact our supplier support team.</p>
        ";

        await SendNotificationAsync(supplierId, NotificationChannel.Email, "SupplierRejected", "Registration Not Approved - SmartInventory", htmlBody, "Supplier", supplierId);
    }

    public async Task SendSuspensionEmailAsync(Guid supplierId, string toEmail, string supplierName, string reason)
    {
        _logger.LogInformation(
            "[SUSPENSION] Suspension email queued for {Email}. Reason: {Reason}.",
            toEmail, reason);

        var htmlBody = $@"
            <h2>Supplier Account Suspended</h2>
            <p>Dear {supplierName},</p>
            <p>We regret to inform you that your supplier account has been suspended.</p>
            <p><strong>Reason:</strong> {reason}</p>
            <p>Please contact our supplier support team if you would like to discuss this decision.</p>
        ";

        await SendNotificationAsync(supplierId, NotificationChannel.Email, "SupplierSuspended", "Account Suspended - SmartInventory", htmlBody, "Supplier", supplierId);
    }

    public async Task SendReactivationEmailAsync(Guid supplierId, string toEmail, string supplierName)
    {
        _logger.LogInformation(
            "[REACTIVATION] Reactivation email queued for {Email}.",
            toEmail);

        var htmlBody = $@"
            <h2>Supplier Account Re-activated</h2>
            <p>Dear {supplierName},</p>
            <p>Good news! Your supplier account has been re-activated.</p>
            <p>You can now access the Supplier Portal and transact with us.</p>
        ";

        await SendNotificationAsync(supplierId, NotificationChannel.Email, "SupplierReactivated", "Account Re-activated - SmartInventory", htmlBody, "Supplier", supplierId);
    }

    public async Task SendPasswordResetRequestAsync(Guid contactId, string toEmail, string resetToken, string contactName)
    {
        var resetLink = $"https://app.smartware.com/supplier/reset-password?token={resetToken}";

        _logger.LogInformation(
            "[PASSWORD_RESET] Reset link queued for {Email}. Link: {Link}.",
            toEmail, resetLink);

        var htmlBody = $@"
            <h2>Reset Your Password - SmartInventory Supplier Portal</h2>
            <p>Dear {contactName},</p>
            <p>You have requested to reset your password. Please click the link below to proceed.</p>
            <p><a href='{resetLink}'>Reset Password</a></p>
            <p>This link will expire in 1 hour.</p>
            <p>If you didn't request this, please ignore this email. Your password will remain unchanged.</p>
        ";

        await SendNotificationAsync(contactId, NotificationChannel.Email, "PasswordResetRequest", "Reset Your Password - SmartInventory", htmlBody, "SupplierContact", contactId);
    }

    public async Task SendPasswordResetSuccessAsync(Guid contactId, string toEmail, string contactName)
    {
        _logger.LogInformation(
            "[PASSWORD_RESET_SUCCESS] Success email queued for {Email}.",
            toEmail);

        var htmlBody = $@"
            <h2>Password Changed Successfully</h2>
            <p>Dear {contactName},</p>
            <p>Your password has been successfully changed.</p>
            <p>If this was not you, please contact our supplier support team immediately.</p>
        ";

        await SendNotificationAsync(contactId, NotificationChannel.Email, "PasswordResetSuccess", "Password Changed Successfully - SmartInventory", htmlBody, "SupplierContact", contactId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SUPPLIER FINANCE NOTIFICATIONS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Dispatched by PurchaseOrderService after ReceiveGoodsAsync completes.
    /// Informs the supplier of the GRN outcome and the exact invoiceable amount.
    /// Uses supplierId as the notification target to go through the Outbox.
    /// </summary>
    public async Task SendSupplierGoodsReceiptNotificationAsync(
        Guid supplierId,
        string supplierEmail,
        string supplierName,
        string poNumber,
        string grnNumber,
        int totalAcceptedQty,
        int totalRejectedQty,
        string? rejectionReasons,
        decimal aggregateAcceptedGrnValue,
        decimal remainingInvoiceableAmount)
    {
        _logger.LogInformation(
            "[GRN_NOTIFY] Goods receipt notification queued for Supplier {SupplierId} ({Email}). GRN: {GrnNumber}, Accepted: {Accepted}, Rejected: {Rejected}.",
            supplierId, supplierEmail, grnNumber, totalAcceptedQty, totalRejectedQty);

        var rejectionBlock = totalRejectedQty > 0
            ? $@"<p style='color:#c0392b;'>
                    <strong>⚠ Rejected Quantity: {totalRejectedQty} unit(s)</strong><br/>
                    <strong>Rejection Reason(s):</strong> {rejectionReasons ?? "Not specified"}
               </p>"
            : "<p style='color:#27ae60;'>✓ All received items were accepted.</p>";

        var htmlBody = $@"
            <h2>Goods Receipt Processed — {poNumber}</h2>
            <p>Dear {supplierName},</p>
            <p>A Goods Receipt (<strong>{grnNumber}</strong>) has been processed for your Purchase Order <strong>{poNumber}</strong>.</p>
            <table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse; font-family:sans-serif;'>
                <tr><td><strong>GRN Number</strong></td><td>{grnNumber}</td></tr>
                <tr><td><strong>Accepted Quantity</strong></td><td style='color:#27ae60;'>{totalAcceptedQty} unit(s)</td></tr>
                <tr><td><strong>Rejected Quantity</strong></td><td style='color:#c0392b;'>{totalRejectedQty} unit(s)</td></tr>
                <tr><td><strong>Aggregate Accepted GRN Value</strong></td><td>₹{aggregateAcceptedGrnValue:N2}</td></tr>
                <tr><td><strong>Remaining Invoiceable Amount</strong></td><td><strong>₹{remainingInvoiceableAmount:N2}</strong></td></tr>
            </table>
            {rejectionBlock}
            <p>Please upload your invoice for <strong>₹{remainingInvoiceableAmount:N2}</strong> via the Supplier Portal.</p>
            <p>If you have any questions, please contact our procurement team.</p>
        ";

        await SendNotificationAsync(
            supplierId,
            NotificationChannel.Email,
            "SupplierGoodsReceived",
            $"Goods Receipt Processed for {poNumber}",
            htmlBody,
            "PurchaseOrder",
            supplierId);
    }

    /// <summary>
    /// Dispatched by InvoiceProcessingService when MatchInvoiceAsync results in IsMatch = false.
    /// Informs the supplier of the exact discrepancy and what amount they should re-invoice for.
    /// </summary>
    public async Task SendSupplierInvoiceRejectedNotificationAsync(
        Guid supplierId,
        string supplierEmail,
        string supplierName,
        string invoiceNumber,
        string poNumber,
        decimal invoiceAmount,
        decimal aggregateAcceptedGrnValue,
        decimal remainingInvoiceableAmount,
        string discrepancyReason)
    {
        _logger.LogInformation(
            "[INVOICE_REJECTED] Invoice rejection notification queued for Supplier {SupplierId} ({Email}). Invoice: {InvoiceNumber}, Reason: {Reason}.",
            supplierId, supplierEmail, invoiceNumber, discrepancyReason);

        var htmlBody = $@"
            <h2>Invoice Not Matched — Action Required</h2>
            <p>Dear {supplierName},</p>
            <p>Your invoice <strong>{invoiceNumber}</strong> for Purchase Order <strong>{poNumber}</strong> could not be matched and has been <strong style='color:#c0392b;'>Rejected</strong>.</p>
            <table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse; font-family:sans-serif;'>
                <tr><td><strong>Invoice Number</strong></td><td>{invoiceNumber}</td></tr>
                <tr><td><strong>Purchase Order</strong></td><td>{poNumber}</td></tr>
                <tr><td><strong>Your Invoice Amount</strong></td><td style='color:#c0392b;'>₹{invoiceAmount:N2}</td></tr>
                <tr><td><strong>Accepted GRN Value</strong></td><td>₹{aggregateAcceptedGrnValue:N2}</td></tr>
                <tr><td><strong>Correct Invoiceable Amount</strong></td><td style='color:#27ae60;'><strong>₹{remainingInvoiceableAmount:N2}</strong></td></tr>
            </table>
            <p><strong>Reason:</strong> {discrepancyReason}</p>
            <p>Please correct your invoice and upload a new document for <strong>₹{remainingInvoiceableAmount:N2}</strong> via the Supplier Portal.</p>
            <p>If you believe this is an error, please contact our finance team with your invoice reference.</p>
        ";

        await SendNotificationAsync(
            supplierId,
            NotificationChannel.Email,
            "SupplierInvoiceRejected",
            $"Invoice {invoiceNumber} — Match Failed for {poNumber}",
            htmlBody,
            "SupplierInvoice",
            supplierId);
    }
}
