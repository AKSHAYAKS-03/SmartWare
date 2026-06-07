using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Service.Services;

/// <summary>
/// Handles supplier invoice upload, history listing, and PDF download.
/// SECURITY: All invoice reads/downloads validate that the invoice SupplierId
/// matches the JWT claim supplierId. Internal finance notes (InternalNotes) are
/// never returned in supplier-facing DTOs.
/// </summary>
public class SupplierInvoiceService : ISupplierInvoiceService
{
    private readonly IUnitOfWork _uow;
    private readonly IFileStorageService _fileStorage;

    public SupplierInvoiceService(IUnitOfWork uow, IFileStorageService fileStorage)
    {
        _uow = uow;
        _fileStorage = fileStorage;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UPLOAD INVOICE
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<SupplierInvoiceDetailDto> UploadInvoiceAsync(Guid supplierId, Guid contactId, SupplierUploadInvoiceRequest request)
    {
        // 1. Validate PO belongs to this supplier — include GRNs and existing invoices for validation
        var po = await _uow.Repository<PurchaseOrder>().Query()
            .Include(p => p.Items)
            .Include(p => p.GoodsReceipts).ThenInclude(g => g.Items)
            .Include(p => p.SupplierInvoices)
            .FirstOrDefaultAsync(p => p.Id == request.PurchaseOrderId && p.SupplierId == supplierId);

        if (po == null)
            throw new NotFoundException("PurchaseOrder", request.PurchaseOrderId);

        if (po.Status == PurchaseOrderStatus.Cancelled || po.Status == PurchaseOrderStatus.Closed)
            throw new BusinessRuleException($"Cannot upload an invoice against a PO in '{po.Status}' status.");

        // 2. Check for duplicate invoice number from this supplier
        var duplicate = await _uow.Repository<SupplierInvoice>().Query()
            .AnyAsync(i => i.SupplierId == supplierId && i.InvoiceNumber == request.InvoiceNumber);

        if (duplicate)
            throw new BusinessRuleException($"An invoice with number '{request.InvoiceNumber}' has already been uploaded.");

        // 3. Over-Billing Prevention
        // Upload Ceiling = MIN(PO Total Amount, Aggregate Accepted GRN Value)
        // This prevents invoicing goods that have not yet been accepted by the warehouse.

        // 3a. Aggregate Accepted GRN Value
        decimal aggregateAcceptedGrnValue = 0;
        var acceptedGrns = po.GoodsReceipts
            .Where(g => g.Status == GoodsReceiptStatus.Accepted || g.Status == GoodsReceiptStatus.PartiallyAccepted);

        foreach (var grn in acceptedGrns)
        {
            foreach (var grnItem in grn.Items)
            {
                var poItem = po.Items.FirstOrDefault(i => i.Id == grnItem.PurchaseOrderItemId);
                if (poItem != null)
                {
                    int accepted = grnItem.QuantityReceived - grnItem.QuantityRejected;
                    aggregateAcceptedGrnValue += accepted * poItem.UnitPrice;
                }
            }
        }

        // 3b. Upload Ceiling = MIN(PO Total, Aggregate Accepted GRN Value)
        decimal uploadCeiling = Math.Min(po.TotalAmount, aggregateAcceptedGrnValue);

        // 3c. Sum all in-flight invoices (Pending, UnderReview, Matched, Paid)
        decimal alreadyCommitted = po.SupplierInvoices
            .Where(i => i.Status == SupplierInvoiceStatus.Pending
                     || i.Status == SupplierInvoiceStatus.UnderReview
                     || i.Status == SupplierInvoiceStatus.Matched
                     || i.Status == SupplierInvoiceStatus.Paid)
            .Sum(i => i.Amount);

        if (alreadyCommitted + request.Amount > uploadCeiling)
            throw new BusinessRuleException(
                $"Invoice upload rejected: The total committed invoice amount " +
                $"(existing: {alreadyCommitted:N2} + new: {request.Amount:N2} = {alreadyCommitted + request.Amount:N2}) " +
                $"would exceed the upload ceiling of {uploadCeiling:N2} " +
                $"(MIN of PO Total {po.TotalAmount:N2} and Aggregate Accepted GRN Value {aggregateAcceptedGrnValue:N2}). " +
                $"Please wait for additional goods to be accepted or adjust your invoice amount.");

        // 4. Save the PDF using the file storage service
        var filePath = await _fileStorage.SaveFileAsync(
            request.FileStream,
            request.FileName,
            "supplier-invoices"
        );

        // 5. Persist the invoice record
        var invoice = new SupplierInvoice
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            PurchaseOrderId = request.PurchaseOrderId,
            UploadedByContactId = contactId,
            InvoiceNumber = request.InvoiceNumber,
            Amount = request.Amount,
            Currency = request.Currency,
            InvoiceDate = request.InvoiceDate,
            FilePath = filePath,
            OriginalFileName = request.FileName,
            Status = SupplierInvoiceStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<SupplierInvoice>().AddAsync(invoice);
        await _uow.CommitAsync();

        return MapToDetailDto(invoice, po.PoNumber);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET MY INVOICES
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<SupplierInvoiceListItemDto>> GetMyInvoicesAsync(Guid supplierId)
    {
        var invoices = await _uow.Repository<SupplierInvoice>().Query()
            .Include(i => i.PurchaseOrder)
            .Where(i => i.SupplierId == supplierId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return invoices.Select(i => new SupplierInvoiceListItemDto(
            Id: i.Id,
            InvoiceNumber: i.InvoiceNumber,
            PoNumber: i.PurchaseOrder?.PoNumber ?? string.Empty,
            Amount: i.Amount,
            Currency: i.Currency,
            InvoiceDate: i.InvoiceDate,
            Status: i.Status,
            CreatedAt: i.CreatedAt
        )).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET INVOICE DETAIL
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<SupplierInvoiceDetailDto> GetInvoiceDetailAsync(Guid supplierId, Guid invoiceId)
    {
        var invoice = await _uow.Repository<SupplierInvoice>().Query()
            .Include(i => i.PurchaseOrder)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.SupplierId == supplierId);

        if (invoice == null)
            throw new NotFoundException("SupplierInvoice", invoiceId);

        return MapToDetailDto(invoice, invoice.PurchaseOrder?.PoNumber ?? string.Empty);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DOWNLOAD INVOICE
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadInvoiceAsync(Guid supplierId, Guid invoiceId)
    {
        var invoice = await _uow.Repository<SupplierInvoice>().Query()
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.SupplierId == supplierId);

        if (invoice == null)
            throw new NotFoundException("SupplierInvoice", invoiceId);

        var stream = await _fileStorage.GetFileStreamAsync(invoice.FilePath);
        return (stream, "application/pdf", invoice.OriginalFileName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Maps an invoice to the supplier-facing detail DTO.
    /// NOTE: InternalNotes are intentionally excluded — suppliers cannot see finance team notes.
    /// </summary>
    private static SupplierInvoiceDetailDto MapToDetailDto(SupplierInvoice invoice, string poNumber)
    {
        return new SupplierInvoiceDetailDto(
            Id: invoice.Id,
            InvoiceNumber: invoice.InvoiceNumber,
            PoNumber: poNumber,
            Amount: invoice.Amount,
            Currency: invoice.Currency,
            InvoiceDate: invoice.InvoiceDate,
            Status: invoice.Status,
            OriginalFileName: invoice.OriginalFileName,
            RejectionReason: invoice.RejectionReason,
            PaidAt: invoice.PaidAt,
            CreatedAt: invoice.CreatedAt
        );
    }
}
