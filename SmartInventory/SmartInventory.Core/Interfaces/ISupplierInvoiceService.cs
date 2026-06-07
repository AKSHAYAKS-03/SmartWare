using SmartInventory.Core.DTOs.SupplierPortal;

namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Invoice operations available to a supplier via the portal.
/// Suppliers can upload, view, and download their own invoices only.
/// </summary>
public interface ISupplierInvoiceService
{
    /// <summary>Uploads a new invoice PDF against a PO. Validates PO belongs to this supplier.</summary>
    Task<SupplierInvoiceDetailDto> UploadInvoiceAsync(Guid supplierId, Guid contactId, SupplierUploadInvoiceRequest request);

    /// <summary>Returns all invoices submitted by this supplier (paginated, most recent first).</summary>
    Task<List<SupplierInvoiceListItemDto>> GetMyInvoicesAsync(Guid supplierId);

    /// <summary>Returns full detail of a single invoice. Validates it belongs to this supplier.</summary>
    Task<SupplierInvoiceDetailDto> GetInvoiceDetailAsync(Guid supplierId, Guid invoiceId);

    /// <summary>Returns a file stream for the invoice PDF download.</summary>
    Task<(Stream Stream, string ContentType, string FileName)> DownloadInvoiceAsync(Guid supplierId, Guid invoiceId);
}
