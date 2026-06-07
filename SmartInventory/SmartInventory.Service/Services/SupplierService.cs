using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;
using Mapster;

namespace SmartInventory.Service.Services;

/// <summary>
/// Supplier directory and relationship management.
///
/// Business rules:
///   — Supplier Code must be unique.
///   — Supplier rating is auto-calculated from SupplierPerformanceLogs (fill rate + on-time %).
///   — Preferred flag per supplier-product ensures only one preferred supplier per product.
///   — Soft-delete blocked if the supplier has open Purchase Orders.
/// </summary>
public class SupplierService : ISupplierService
{
    private readonly IUnitOfWork _uow;
    private readonly IEmailService _emailService;

    public SupplierService(IUnitOfWork uow, IEmailService emailService)
    {
        _uow = uow;
        _emailService = emailService;
    }

    // ─── Supplier CRUD ────────────────────────────────────────────────────────

    public async Task<SupplierResponseDto> UpdateSupplierAsync(Guid supplierId, SupplierUpdateDto dto)
    {
        var supplier = await _uow.Repository<Supplier>().GetByIdAsync(supplierId);
        if (supplier == null) throw new NotFoundException("Supplier", supplierId);

        // Check code uniqueness excluding self
        bool codeConflict = await _uow.Repository<Supplier>()
            .Query().AnyAsync(s => s.Code == dto.Code && s.Id != supplierId);
        if (codeConflict)
            throw new BusinessRuleException($"Supplier code '{dto.Code}' is already used by another supplier.");

        supplier.Name = dto.Name;
        supplier.Code = dto.Code;
        supplier.GSTIN = dto.GSTIN;
        supplier.PAN = dto.PAN;
        supplier.ContactPerson = dto.ContactPerson;
        supplier.Email = dto.Email ?? supplier.Email;
        supplier.Phone = dto.Phone;
        supplier.Address = dto.Address ?? supplier.Address;
        supplier.LeadTimeDays = dto.LeadTimeDays;
        supplier.PaymentTerms = dto.PaymentTerms;
        supplier.CreditLimit = dto.CreditLimit;
        supplier.IsActive = dto.IsActive;

        _uow.Repository<Supplier>().Update(supplier);
        await _uow.CommitAsync();
        return supplier.Adapt<SupplierResponseDto>();
    }

    public async Task DeleteSupplierAsync(Guid supplierId)
    {
        var supplier = await _uow.Repository<Supplier>().GetByIdAsync(supplierId);
        if (supplier == null) throw new NotFoundException("Supplier", supplierId);

        bool hasOpenPOs = await _uow.Repository<PurchaseOrder>()
            .Query()
            .AnyAsync(po => po.SupplierId == supplierId &&
                            po.Status != Core.Enums.PurchaseOrderStatus.Closed &&
                            po.Status != Core.Enums.PurchaseOrderStatus.Rejected);

        if (hasOpenPOs)
            throw new BusinessRuleException(
                "Cannot delete a supplier with open or in-progress purchase orders.");

        _uow.Repository<Supplier>().Delete(supplier);
        await _uow.CommitAsync();
    }

    public async Task<SupplierResponseDto> GetSupplierByIdAsync(Guid supplierId)
    {
        var supplier = await _uow.Repository<Supplier>().GetByIdAsync(supplierId);
        if (supplier == null) throw new NotFoundException("Supplier", supplierId);
        return supplier.Adapt<SupplierResponseDto>();
    }

    public async Task<PagedResult<SupplierResponseDto>> GetSuppliersAsync(SupplierQueryParameters queryParams)
    {
        var query = _uow.Repository<Supplier>().Query();

        if (queryParams.IsActive.HasValue)
            query = query.Where(s => s.IsActive == queryParams.IsActive.Value);

        if (queryParams.MinRating.HasValue)
            query = query.Where(s => s.Rating >= queryParams.MinRating.Value);

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
            query = query.Where(s => s.Name.Contains(queryParams.Search) ||
                                     s.Code.Contains(queryParams.Search) ||
                                     (s.Email != null && s.Email.Contains(queryParams.Search)));

        int total = await query.CountAsync();
        query = query.OrderBy(s => s.Name);

        var data = await query
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync();

        return new PagedResult<SupplierResponseDto>
        {
            Data = data.Adapt<IEnumerable<SupplierResponseDto>>(),
            TotalCount = total,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    // ─── Supplier-Product Mappings ────────────────────────────────────────────

    public async Task<SupplierProductResponseDto> AddSupplierProductAsync(SupplierProductCreateDto dto)
    {
        var supplier = await _uow.Repository<Supplier>().GetByIdAsync(dto.SupplierId);
        if (supplier == null) throw new NotFoundException("Supplier", dto.SupplierId);

        var product = await _uow.Repository<Product>().GetByIdAsync(dto.ProductId);
        if (product == null) throw new NotFoundException("Product", dto.ProductId);

        // If setting as preferred, clear existing preferred flag for this product
        if (dto.IsPreferred)
        {
            var existing = await _uow.Repository<SupplierProduct>()
                .Query()
                .Where(sp => sp.ProductId == dto.ProductId && sp.IsPreferred)
                .ToListAsync();
            foreach (var sp in existing)
            {
                sp.IsPreferred = false;
                _uow.Repository<SupplierProduct>().Update(sp);
            }
        }

        var supplierProduct = new SupplierProduct
        {
            Id = Guid.NewGuid(),
            SupplierId = dto.SupplierId,
            ProductId = dto.ProductId,
            UnitPrice = dto.UnitPrice,
            LeadTimeDays = dto.LeadTimeDays,
            MinOrderQuantity = dto.MinOrderQuantity,
            IsPreferred = dto.IsPreferred,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<SupplierProduct>().AddAsync(supplierProduct);
        await _uow.CommitAsync();

        return supplierProduct.Adapt<SupplierProductResponseDto>();
    }

    public async Task<SupplierProductResponseDto> UpdateSupplierProductAsync(
        Guid supplierProductId, SupplierProductUpdateDto dto)
    {
        var sp = await _uow.Repository<SupplierProduct>()
            .Query()
            .Include(x => x.Supplier)
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == supplierProductId);

        if (sp == null) throw new NotFoundException("SupplierProduct", supplierProductId);

        if (dto.IsPreferred && !sp.IsPreferred)
        {
            var existing = await _uow.Repository<SupplierProduct>()
                .Query()
                .Where(x => x.ProductId == sp.ProductId && x.IsPreferred && x.Id != supplierProductId)
                .ToListAsync();
            foreach (var other in existing)
            {
                other.IsPreferred = false;
                _uow.Repository<SupplierProduct>().Update(other);
            }
        }

        sp.UnitPrice = dto.UnitPrice;
        sp.LeadTimeDays = dto.LeadTimeDays;
        sp.MinOrderQuantity = dto.MinOrderQuantity;
        sp.IsPreferred = dto.IsPreferred;

        _uow.Repository<SupplierProduct>().Update(sp);
        await _uow.CommitAsync();

        return sp.Adapt<SupplierProductResponseDto>();
    }

    public async Task RemoveSupplierProductAsync(Guid supplierProductId)
    {
        var sp = await _uow.Repository<SupplierProduct>().GetByIdAsync(supplierProductId);
        if (sp == null) throw new NotFoundException("SupplierProduct", supplierProductId);
        _uow.Repository<SupplierProduct>().Delete(sp);
        await _uow.CommitAsync();
    }

    public async Task<IEnumerable<SupplierProductResponseDto>> GetSupplierProductsAsync(Guid supplierId)
    {
        var items = await _uow.Repository<SupplierProduct>()
            .Query()
            .Include(sp => sp.Supplier)
            .Include(sp => sp.Product)
            .Where(sp => sp.SupplierId == supplierId)
            .ToListAsync();

        return items.Adapt<IEnumerable<SupplierProductResponseDto>>();
    }

    // ─── Performance ──────────────────────────────────────────────────────────

    public async Task<IEnumerable<SupplierPerformanceLogResponseDto>> GetSupplierPerformanceAsync(Guid supplierId)
    {
        var logs = await _uow.Repository<SupplierPerformanceLog>()
            .Query()
            .Include(l => l.Supplier)
            .Include(l => l.PurchaseOrder)
            .Where(l => l.SupplierId == supplierId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return logs.Select(l => new SupplierPerformanceLogResponseDto
        {
            Id = l.Id,
            SupplierId = l.SupplierId,
            SupplierName = l.Supplier.Name,
            PurchaseOrderId = l.PurchaseOrderId,
            PurchaseOrderNumber = l.PurchaseOrder.PoNumber,
            PromisedDays = l.PromisedDays,
            ActualDays = l.ActualDays,
            FillRate = l.FillRate,
            Notes = l.Notes,
            CreatedAt = l.CreatedAt
        });
    }

    /// <summary>
    /// Recalculates and persists the supplier's performance rating based on historical logs.
    /// Formula: Rating = (Average Fill Rate * 0.6) + (On-Time Delivery % * 0.4)
    /// Scale: 0.0 – 5.0
    /// Called automatically after each PO is closed/received.
    /// </summary>
    public async Task RecalculateSupplierRatingAsync(Guid supplierId)
    {
        var supplier = await _uow.Repository<Supplier>().GetByIdAsync(supplierId);
        if (supplier == null) return;

        var logs = await _uow.Repository<SupplierPerformanceLog>()
            .Query()
            .Where(l => l.SupplierId == supplierId)
            .ToListAsync();

        if (!logs.Any()) return;

        double avgFillRate = (double)logs.Average(l => l.FillRate);
        double onTimeCount = logs.Count(l => l.ActualDays <= l.PromisedDays);
        double onTimePct = (onTimeCount / logs.Count) * 100.0;

        // Composite score normalised to 0–5 scale
        supplier.Rating = (decimal)Math.Round(
            (avgFillRate * 0.6 + onTimePct * 0.4) / 20.0, 2);

        _uow.Repository<Supplier>().Update(supplier);
        await _uow.CommitAsync();
    }



    // ─────────────────────────────────────────────────────────────────────────
    // INVITE SUPPLIER (Admin invite)
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<SupplierResponseDto> InviteSupplierAsync(SupplierInviteRequest request)
    {
        // Validate duplicate GSTIN
        bool gstinExists = await _uow.Repository<Supplier>()
            .Query().AnyAsync(s => s.GSTIN == request.GSTIN && s.IsActive);
        if (gstinExists)
            throw new BusinessRuleException($"A supplier with GSTIN '{request.GSTIN}' already exists.");



        // Validate duplicate Email
        bool emailExists = await _uow.Repository<Supplier>()
            .Query().AnyAsync(s => s.Email == request.Email && s.IsActive);
        if (emailExists)
            throw new BusinessRuleException($"A supplier with email '{request.Email}' already exists.");

        // Validate duplicate Phone
        bool phoneExists = await _uow.Repository<Supplier>()
            .Query().AnyAsync(s => s.Phone == request.Phone && s.IsActive);
        if (phoneExists)
            throw new BusinessRuleException($"A supplier with phone '{request.Phone}' already exists.");

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            GSTIN = request.GSTIN,
            PAN = null,
            ContactPerson = "Invited Supplier Contact",
            Email = request.Email,
            Phone = request.Phone,
            Address = "Pending Registration",
            LeadTimeDays = 0,
            PaymentTerms = PaymentTerms.Net30, // Default until review
            CreditLimit = 0,
            Rating = 0,
            IsActive = true,
            Status = SupplierStatus.InviteSent,
            RegistrationSource = RegistrationSource.AdminInvited,
            InviteToken = Guid.NewGuid().ToString("N"),
            InviteTokenExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<Supplier>().AddAsync(supplier);

        // Also create a dummy contact for them associated with their email
        var contact = new SupplierContact
        {
            Id = Guid.NewGuid(),
            SupplierId = supplier.Id,
            FullName = "Invited Supplier Contact",
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"), workFactor: 12), // Dummy secure password
            Phone = request.Phone,
            IsActive = true,
            EmailVerified = true, // Invited skips OTP verification
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<SupplierContact>().AddAsync(contact);
        await _uow.CommitAsync();

        // Send invite email with the registration link
        var inviteLink = $"https://smartinventory.app/supplier/complete-registration?token={supplier.InviteToken}";
        var htmlBody = $@"
            <h2>You have been invited to SmartInventory!</h2>
            <p>Hi,</p>
            <p>You have been invited to join the SmartInventory Supplier Portal as a supplier ({supplier.Name}).</p>
            <p>Please click the link below to set your password and complete your registration profile. <strong>This link expires in 7 days.</strong></p>
            <p><a href='{inviteLink}' style='background:#4f46e5;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;'>Complete Registration</a></p>
            <p>If you believe this email was sent in error, you can safely ignore it.</p>
        ";
        await _emailService.SendEmailAsync(request.Email, "You're invited to SmartInventory Supplier Portal", htmlBody);

        return supplier.Adapt<SupplierResponseDto>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // REVIEW SUPPLIER (Admin action)
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<SupplierResponseDto> ReviewSupplierAsync(Guid supplierId, SupplierReviewRequest request)
    {
        var supplier = await _uow.Repository<Supplier>().Query()
            .FirstOrDefaultAsync(s => s.Id == supplierId && s.IsActive);

        if (supplier == null)
            throw new NotFoundException("Supplier", supplierId);

        if (supplier.Status != SupplierStatus.PendingReview)
            throw new BusinessRuleException($"Supplier is not in pending review state (current: {supplier.Status}).");

        if (request.Action.Equals("Approve", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.Code))
                throw new BusinessRuleException("Supplier Code is required for approval.");

            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Code, "^[A-Z0-9-]{3,20}$"))
                throw new BusinessRuleException("Supplier Code must be 3-20 characters long, containing uppercase letters, numbers, or hyphens.");

            bool codeConflict = await _uow.Repository<Supplier>()
                .Query().AnyAsync(s => s.Code == request.Code && s.Id != supplierId);
            if (codeConflict)
                throw new BusinessRuleException($"Supplier code '{request.Code}' is already used by another supplier.");

            supplier.Status = SupplierStatus.AgreementPending;
            supplier.Code = request.Code;
            supplier.CreditLimit = request.CreditLimit ?? 0;
            supplier.PaymentTerms = request.PaymentTerms ?? PaymentTerms.Net30;
        }
        else if (request.Action.Equals("Reject", StringComparison.OrdinalIgnoreCase))
        {
            supplier.Status = SupplierStatus.Rejected;
            supplier.RejectionReason = request.Reason ?? "Application rejected by administrator.";
        }
        else if (request.Action.Equals("RequestMoreInfo", StringComparison.OrdinalIgnoreCase))
        {
            supplier.Status = SupplierStatus.InfoRequested;
            supplier.InfoRequestedMessage = request.Reason ?? "More information is requested for your registration profile.";
        }
        else
        {
            throw new BusinessRuleException($"Invalid action '{request.Action}'. Allowed actions: Approve, Reject, RequestMoreInfo.");
        }

        _uow.Repository<Supplier>().Update(supplier);
        await _uow.CommitAsync();

        // Send status-change notification email
        if (request.Action.Equals("Approve", StringComparison.OrdinalIgnoreCase))
        {
            var htmlBody = $@"
                <h2>Your Supplier Application has been Approved!</h2>
                <p>Hi {supplier.Name},</p>
                <p>Great news! Your supplier application has been reviewed and approved by our team.</p>
                <p>Your Supplier Code is: <strong>{supplier.Code}</strong></p>
                <p>Your Credit Limit is: <strong>₹{supplier.CreditLimit:N2}</strong></p>
                <p>Please log in to the Supplier Portal to sign your agreement and activate your account.</p>
                <p><a href='https://smartinventory.app/supplier/login'>Log in to Supplier Portal</a></p>
            ";
            await _emailService.SendEmailAsync(supplier.Email, "SmartInventory - Supplier Application Approved", htmlBody);
        }
        else if (request.Action.Equals("Reject", StringComparison.OrdinalIgnoreCase))
        {
            var htmlBody = $@"
                <h2>Your Supplier Application Status</h2>
                <p>Hi {supplier.Name},</p>
                <p>We regret to inform you that your supplier application has not been approved at this time.</p>
                <p><strong>Reason:</strong> {supplier.RejectionReason}</p>
                <p>If you believe this decision is in error, please contact our procurement team.</p>
            ";
            await _emailService.SendEmailAsync(supplier.Email, "SmartInventory - Supplier Application Update", htmlBody);
        }
        else if (request.Action.Equals("RequestMoreInfo", StringComparison.OrdinalIgnoreCase))
        {
            var htmlBody = $@"
                <h2>Additional Information Required</h2>
                <p>Hi {supplier.Name},</p>
                <p>Our team has reviewed your application and requires additional information before we can proceed.</p>
                <p><strong>Details:</strong> {supplier.InfoRequestedMessage}</p>
                <p>Please log in to the Supplier Portal to provide the requested information.</p>
                <p><a href='https://smartinventory.app/supplier/login'>Log in to Supplier Portal</a></p>
            ";
            await _emailService.SendEmailAsync(supplier.Email, "SmartInventory - Additional Information Required", htmlBody);
        }

        return supplier.Adapt<SupplierResponseDto>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SUSPEND SUPPLIER
    // ─────────────────────────────────────────────────────────────────────────
    public async Task SuspendSupplierAsync(Guid supplierId, string reason)
    {
        var supplier = await _uow.Repository<Supplier>().GetByIdAsync(supplierId);
        if (supplier == null) throw new NotFoundException("Supplier", supplierId);

        supplier.Status = SupplierStatus.Suspended;
        supplier.SuspensionReason = reason;

        _uow.Repository<Supplier>().Update(supplier);
        await _uow.CommitAsync();

        // Notify supplier of suspension
        var htmlBody = $@"
            <h2>Your Supplier Portal Access has been Suspended</h2>
            <p>Hi {supplier.Name},</p>
            <p>Your SmartInventory Supplier Portal account has been suspended by our administrator.</p>
            <p><strong>Reason:</strong> {supplier.SuspensionReason}</p>
            <p>You will not be able to log in until your account is reactivated. Please contact our procurement team if you have any questions.</p>
        ";
        await _emailService.SendEmailAsync(supplier.Email, "SmartInventory - Portal Access Suspended", htmlBody);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ACTIVATE SUPPLIER
    // ─────────────────────────────────────────────────────────────────────────
    public async Task ActivateSupplierAsync(Guid supplierId)
    {
        var supplier = await _uow.Repository<Supplier>().GetByIdAsync(supplierId);
        if (supplier == null) throw new NotFoundException("Supplier", supplierId);

        supplier.Status = SupplierStatus.Active;
        supplier.SuspensionReason = null;
        supplier.RejectionReason = null;

        _uow.Repository<Supplier>().Update(supplier);
        await _uow.CommitAsync();

        // Notify supplier of reactivation
        var htmlBody = $@"
            <h2>Your Supplier Portal Access has been Restored</h2>
            <p>Hi {supplier.Name},</p>
            <p>Your SmartInventory Supplier Portal account has been reactivated. You can now log in and resume activity.</p>
            <p><a href='https://smartinventory.app/supplier/login' style='background:#4f46e5;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;'>Log in to Supplier Portal</a></p>
        ";
        await _emailService.SendEmailAsync(supplier.Email, "SmartInventory - Portal Access Restored", htmlBody);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET PENDING REVIEWS
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<IEnumerable<SupplierResponseDto>> GetPendingReviewsAsync()
    {
        var pending = await _uow.Repository<Supplier>().Query()
            .Where(s => s.Status == SupplierStatus.PendingReview && s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();

        return pending.Adapt<IEnumerable<SupplierResponseDto>>();
    }
}
