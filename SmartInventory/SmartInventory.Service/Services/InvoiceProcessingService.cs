using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Service.Services;

public class InvoiceProcessingService : IInvoiceProcessingService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notificationService;

    public InvoiceProcessingService(IUnitOfWork uow, INotificationService notificationService)
    {
        _uow = uow;
        _notificationService = notificationService;
    }

    public async Task MarkUnderReviewAsync(Guid invoiceId, InvoiceActionDto dto)
    {
        var invoice = await _uow.Repository<SupplierInvoice>().GetByIdAsync(invoiceId);
        if (invoice == null) throw new NotFoundException("Invoice", invoiceId);

        if (invoice.Status != SupplierInvoiceStatus.Pending)
            throw new BusinessRuleException("Only Pending invoices can be marked as Under Review.");

        invoice.Status = SupplierInvoiceStatus.UnderReview;
        if (!string.IsNullOrEmpty(dto.InternalNotes))
        {
            invoice.InternalNotes = string.IsNullOrEmpty(invoice.InternalNotes)
                ? dto.InternalNotes
                : $"{invoice.InternalNotes}\n{dto.InternalNotes}";
        }

        _uow.Repository<SupplierInvoice>().Update(invoice);
        await _uow.CommitAsync();
    }

    public async Task<InvoiceMatchResultDto> MatchInvoiceAsync(Guid invoiceId, InvoiceActionDto dto)
    {
        var invoice = await _uow.Repository<SupplierInvoice>()
            .Query()
            .Include(i => i.PurchaseOrder)
                .ThenInclude(po => po.Items)
            .Include(i => i.PurchaseOrder)
                .ThenInclude(po => po.GoodsReceipts)
                    .ThenInclude(grn => grn.Items)
            .Include(i => i.PurchaseOrder)
                .ThenInclude(po => po.SupplierInvoices)
            .Include(i => i.PurchaseOrder)
                .ThenInclude(po => po.Supplier)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice == null) throw new NotFoundException("Invoice", invoiceId);

        if (invoice.Status != SupplierInvoiceStatus.Pending && invoice.Status != SupplierInvoiceStatus.UnderReview)
            throw new BusinessRuleException("Only Pending or UnderReview invoices can be matched.");

        var po = invoice.PurchaseOrder;

        // ── Formula: Aggregate Accepted GRN Value ────────────────────────────
        // SUM((QuantityReceived - QuantityRejected) × POItem.UnitPrice)
        // for all Accepted / PartiallyAccepted GRNs
        decimal aggregateAcceptedGrnValue = 0;
        foreach (var grn in po.GoodsReceipts.Where(g => g.Status == GoodsReceiptStatus.Accepted || g.Status == GoodsReceiptStatus.PartiallyAccepted))
        {
            foreach (var grnItem in grn.Items)
            {
                var poItem = po.Items.FirstOrDefault(i => i.Id == grnItem.PurchaseOrderItemId);
                if (poItem != null)
                {
                    int acceptedQty = grnItem.QuantityReceived - grnItem.QuantityRejected;
                    aggregateAcceptedGrnValue += acceptedQty * poItem.UnitPrice;
                }
            }
        }

        // ── Formula: Already Matched/Paid Invoice Amount ─────────────────────
        // SUM(ApprovedAmount) WHERE Status IN (Matched, Paid) AND InvoiceId != current
        decimal alreadyMatchedPaidAmount = po.SupplierInvoices
            .Where(i => i.Id != invoiceId
                     && (i.Status == SupplierInvoiceStatus.Matched || i.Status == SupplierInvoiceStatus.Paid))
            .Sum(i => i.ApprovedAmount ?? 0);

        decimal invoiceTotal = invoice.Amount;

        // ── Formula: Remaining Invoiceable Amount ────────────────────────────
        // AggregateAcceptedGrnValue - AlreadyMatchedPaidAmount
        decimal remainingInvoiceableAmount = aggregateAcceptedGrnValue - alreadyMatchedPaidAmount;

        var result = new InvoiceMatchResultDto
        {
            InvoiceId = invoiceId,
            IsMatch = true
        };

        // ── Match Validation: Aggregate Ceiling Check ────────────────────────
        // Rule: (AlreadyMatchedPaid + CurrentInvoiceAmount) <= AggregateAcceptedGrnValue
        // This correctly supports partial invoicing across multiple GRNs.
        if (alreadyMatchedPaidAmount + invoiceTotal > aggregateAcceptedGrnValue)
        {
            result.IsMatch = false;
            result.Discrepancies.Add(
                $"Invoice amount ({invoiceTotal:N2}) causes the total paid/matched amount " +
                $"({alreadyMatchedPaidAmount:N2} existing + {invoiceTotal:N2} current = {alreadyMatchedPaidAmount + invoiceTotal:N2}) " +
                $"to exceed the Aggregate Accepted GRN Value ({aggregateAcceptedGrnValue:N2}). " +
                $"Remaining invoiceable amount is {remainingInvoiceableAmount:N2}.");
        }

        // ── Safety Guard: Cannot exceed PO Total Amount ───────────────────────
        if (invoiceTotal > po.TotalAmount)
        {
            result.IsMatch = false;
            result.Discrepancies.Add(
                $"Invoice amount ({invoiceTotal:N2}) exceeds the Purchase Order total ({po.TotalAmount:N2}).");
        }

        if (result.IsMatch)
        {
            invoice.Status = SupplierInvoiceStatus.Matched;
            invoice.ApprovedAmount = invoiceTotal;
            result.ApprovedAmount = invoiceTotal;
        }
        else
        {
            invoice.Status = SupplierInvoiceStatus.Rejected;
            invoice.InternalNotes = string.IsNullOrEmpty(invoice.InternalNotes)
                ? string.Join("\n", result.Discrepancies)
                : $"{invoice.InternalNotes}\n{string.Join("\n", result.Discrepancies)}";
            var supplier = po.Supplier;
            if (supplier != null)
            {
                var discrepancyReason = string.Join(" | ", result.Discrepancies);
                invoice.RejectionReason = discrepancyReason;
            }
        }

        if (!string.IsNullOrEmpty(dto.InternalNotes))
        {
            invoice.InternalNotes = string.IsNullOrEmpty(invoice.InternalNotes)
                ? dto.InternalNotes
                : $"{invoice.InternalNotes}\n{dto.InternalNotes}";
        }

        _uow.Repository<SupplierInvoice>().Update(invoice);
        await _uow.CommitAsync();

        if (result.IsMatch)
        {
            await _notificationService.SendInvoiceApprovedAlertAsync(invoice.Id);
        }
        else if (!string.IsNullOrEmpty(invoice.RejectionReason))
        {
            await _notificationService.SendInvoiceRejectedAlertAsync(invoice.Id, invoice.RejectionReason);
        }

        return result;
    }

    public async Task PayInvoiceAsync(Guid invoiceId, InvoicePayDto dto)
    {
        var invoice = await _uow.Repository<SupplierInvoice>().GetByIdAsync(invoiceId);
        if (invoice == null) throw new NotFoundException("Invoice", invoiceId);

        if (invoice.Status != SupplierInvoiceStatus.Matched)
        {
            await _notificationService.SendInvoicePaymentFailedAlertAsync(invoice.Id, "Only Matched invoices can be paid.");
            throw new BusinessRuleException("Only Matched invoices can be paid.");
        }

        try
        {
            invoice.Status = SupplierInvoiceStatus.Paid;
            invoice.PaidAmount = invoice.ApprovedAmount;
            invoice.PaymentReference = dto.PaymentReference;
            invoice.PaidAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(dto.InternalNotes))
            {
                invoice.InternalNotes = string.IsNullOrEmpty(invoice.InternalNotes)
                    ? dto.InternalNotes
                    : $"{invoice.InternalNotes}\n{dto.InternalNotes}";
            }

            _uow.Repository<SupplierInvoice>().Update(invoice);
            await _uow.CommitAsync();

            await _notificationService.SendInvoicePaymentCompletedAlertAsync(invoice.Id);
        }
        catch (Exception ex)
        {
            await _notificationService.SendInvoicePaymentFailedAlertAsync(invoice.Id, ex.Message);
            throw;
        }
    }

    public async Task VoidInvoiceAsync(Guid invoiceId, InvoiceRejectDto dto)
    {
        var invoice = await _uow.Repository<SupplierInvoice>().GetByIdAsync(invoiceId);
        if (invoice == null) throw new NotFoundException("Invoice", invoiceId);

        if (invoice.Status == SupplierInvoiceStatus.Paid)
            throw new BusinessRuleException("Paid invoices cannot be voided.");

        invoice.Status = SupplierInvoiceStatus.Voided;
        invoice.RejectionReason = dto.RejectionReason;

        if (!string.IsNullOrEmpty(dto.InternalNotes))
        {
            invoice.InternalNotes = string.IsNullOrEmpty(invoice.InternalNotes)
                ? dto.InternalNotes
                : $"{invoice.InternalNotes}\n{dto.InternalNotes}";
        }

        _uow.Repository<SupplierInvoice>().Update(invoice);
        await _uow.CommitAsync();
    }
}
