using SmartInventory.Core.DTOs.SupplierPortal;

namespace SmartInventory.Core.Interfaces;
public interface ISupplierInvoiceService
{
    Task<SupplierInvoiceDetailDto> UploadInvoiceAsync(Guid supplierId, Guid contactId, SupplierUploadInvoiceRequest request);

    Task<List<SupplierInvoiceListItemDto>> GetMyInvoicesAsync(Guid supplierId);

    Task<SupplierInvoiceDetailDto> GetInvoiceDetailAsync(Guid supplierId, Guid invoiceId);

    Task<(Stream Stream, string ContentType, string FileName)> DownloadInvoiceAsync(Guid supplierId, Guid invoiceId);
}
