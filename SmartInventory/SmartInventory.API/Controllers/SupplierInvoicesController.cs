using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Interfaces;
using System.Security.Claims;

namespace SmartInventory.API.Controllers;

/// <summary>
/// Supplier portal invoice endpoints.
/// Route prefix: /api/supplier/invoices
///
/// POST   /api/supplier/invoices           — Upload invoice PDF [Supplier]
/// GET    /api/supplier/invoices           — List my invoices [Supplier]
/// GET    /api/supplier/invoices/{id}      — Invoice detail [Supplier]
/// GET    /api/supplier/invoices/{id}/download — Download PDF [Supplier]
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/supplier/invoices")]
[Authorize(Policy = "RequireSupplier")]
public class SupplierInvoicesController : ControllerBase
{
    private readonly ISupplierInvoiceService _service;

    public SupplierInvoicesController(ISupplierInvoiceService service)
    {
        _service = service;
    }

    private Guid GetSupplierId() => Guid.Parse(User.FindFirstValue("supplierId")!);
    private Guid GetContactId() => Guid.Parse(User.FindFirstValue("contactId")!);

    /// <summary>
    /// Uploads an invoice PDF against a purchase order.
    /// Accepts multipart/form-data. Max file size: 10 MB, PDF only.
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(
        [FromForm] Guid purchaseOrderId,
        [FromForm] string invoiceNumber,
        [FromForm] decimal amount,
        [FromForm] string currency,
        [FromForm] DateTime invoiceDate,
        IFormFile file)
    {
        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { detail = "File size exceeds the 10 MB limit." });

        if (file.ContentType != "application/pdf")
            return BadRequest(new { detail = "Only PDF files are allowed." });
        var request = new SupplierUploadInvoiceRequest(
            PurchaseOrderId: purchaseOrderId,
            InvoiceNumber: invoiceNumber,
            Amount: amount,
            Currency: currency,
            InvoiceDate: invoiceDate,
            FileStream: file.OpenReadStream(),
            FileName: file.FileName,
            ContentType: file.ContentType
        );
        var result = await _service.UploadInvoiceAsync(GetSupplierId(), GetContactId(), request);
        return CreatedAtAction(nameof(GetDetail), new { id = result.Id }, result);
    }

    /// <summary>
    /// Returns a list of all invoices submitted by this supplier.
    /// Ordered by most recent first. Internal finance notes are NOT included.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyInvoices()
    {
        var result = await _service.GetMyInvoicesAsync(GetSupplierId());
        return Ok(result);
    }

    /// <summary>
    /// Returns full detail of a single invoice.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var result = await _service.GetInvoiceDetailAsync(GetSupplierId(), id);
        return Ok(result);
    }

    /// <summary>
    /// Returns the invoice PDF file as a downloadable stream.
    /// Content-Disposition: attachment.
    /// </summary>
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var (stream, contentType, fileName) = await _service.DownloadInvoiceAsync(GetSupplierId(), id);
        return File(stream, contentType, fileName);
    }
}
