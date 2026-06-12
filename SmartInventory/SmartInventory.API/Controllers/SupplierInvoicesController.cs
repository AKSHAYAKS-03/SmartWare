using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Interfaces;
using System.Security.Claims;

namespace SmartInventory.API.Controllers;


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


    [EnableRateLimiting("mutations")]
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(
        [FromForm] Guid purchaseOrderId,
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


    [HttpGet]
    public async Task<IActionResult> GetMyInvoices()
    {
        var result = await _service.GetMyInvoicesAsync(GetSupplierId());
        return Ok(result);
    }


    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var result = await _service.GetInvoiceDetailAsync(GetSupplierId(), id);
        return Ok(result);
    }


    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var (stream, contentType, fileName) = await _service.DownloadInvoiceAsync(GetSupplierId(), id);
        return File(stream, contentType, fileName);
    }
}
