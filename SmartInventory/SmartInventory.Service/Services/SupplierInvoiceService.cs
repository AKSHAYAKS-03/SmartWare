using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Service.Services;
public class SupplierInvoiceService : ISupplierInvoiceService
{
    private readonly IUnitOfWork _uow;
    private readonly IFileStorageService _fileStorage;
    private readonly INotificationService _notificationService;
    private readonly ISequenceNumberGenerator _sequenceNumberGenerator;

    public SupplierInvoiceService(IUnitOfWork uow, IFileStorageService fileStorage, INotificationService notificationService, ISequenceNumberGenerator sequenceNumberGenerator)
    {
        _uow = uow;
        _fileStorage = fileStorage;
        _notificationService = notificationService;
        _sequenceNumberGenerator = sequenceNumberGenerator;
    }


    public async Task<SupplierInvoiceDetailDto> UploadInvoiceAsync(Guid supplierId, Guid contactId, SupplierUploadInvoiceRequest request)
    {

        var po = await _uow.Repository<PurchaseOrder>().Query()
            .Include(p => p.Items)
            .Include(p => p.GoodsReceipts).ThenInclude(g => g.Items)
            .Include(p => p.SupplierInvoices)
            .FirstOrDefaultAsync(p => p.Id == request.PurchaseOrderId && p.SupplierId == supplierId);

        if (po == null)
            throw new NotFoundException("PurchaseOrder", request.PurchaseOrderId);

        if (po.Status == PurchaseOrderStatus.Cancelled || po.Status == PurchaseOrderStatus.Closed)
            throw new BusinessRuleException($"Cannot upload an invoice against a PO in '{po.Status}' status.");






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


        decimal uploadCeiling = Math.Min(po.TotalAmount, aggregateAcceptedGrnValue);


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


        var filePath = await _fileStorage.SaveFileAsync(
            request.FileStream,
            request.FileName,
            "supplier-invoices",
            $"SUPINV_{po.PoNumber}"
        );


        var invoiceNumber = await _sequenceNumberGenerator.GenerateAsync("seq_invoices", "INV");
        var invoice = new SupplierInvoice
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            PurchaseOrderId = request.PurchaseOrderId,
            UploadedByContactId = contactId,
            InvoiceNumber = invoiceNumber,
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

        await _notificationService.SendInvoiceUploadedAlertAsync(invoice.Id);

        return MapToDetailDto(invoice, po.PoNumber);
    }


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


    public async Task<SupplierInvoiceDetailDto> GetInvoiceDetailAsync(Guid supplierId, Guid invoiceId)
    {
        var invoice = await _uow.Repository<SupplierInvoice>().Query()
            .Include(i => i.PurchaseOrder)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.SupplierId == supplierId);

        if (invoice == null)
            throw new NotFoundException("SupplierInvoice", invoiceId);

        return MapToDetailDto(invoice, invoice.PurchaseOrder?.PoNumber ?? string.Empty);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadInvoiceAsync(Guid supplierId, Guid invoiceId)
    {
        var invoice = await _uow.Repository<SupplierInvoice>().Query()
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.SupplierId == supplierId);

        if (invoice == null)
            throw new NotFoundException("SupplierInvoice", invoiceId);

        var stream = await _fileStorage.GetFileStreamAsync(invoice.FilePath);
        return (stream, "application/pdf", invoice.OriginalFileName);
    }


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
