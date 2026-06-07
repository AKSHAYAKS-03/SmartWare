using System;
using System.Threading.Tasks;
using SmartInventory.Core.DTOs;

namespace SmartInventory.Core.Interfaces;

public interface IInvoiceProcessingService
{
    Task MarkUnderReviewAsync(Guid invoiceId, InvoiceActionDto dto);
    Task<InvoiceMatchResultDto> MatchInvoiceAsync(Guid invoiceId, InvoiceActionDto dto);
    Task PayInvoiceAsync(Guid invoiceId, InvoicePayDto dto);
    Task VoidInvoiceAsync(Guid invoiceId, InvoiceRejectDto dto);
}
