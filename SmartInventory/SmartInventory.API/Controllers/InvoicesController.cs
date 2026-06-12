using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Interfaces;
using System;
using System.Threading.Tasks;

namespace SmartInventory.API.Controllers;


[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = "RequireManager")]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceProcessingService _service;

    public InvoicesController(IInvoiceProcessingService service)
    {
        _service = service;
    }

    [EnableRateLimiting("mutations")]

    [HttpPost("{id:guid}/under-review")]
    public async Task<IActionResult> MarkUnderReview(Guid id, [FromBody] InvoiceActionDto dto)
    {
        await _service.MarkUnderReviewAsync(id, dto);
        return NoContent();
    }

    [EnableRateLimiting("mutations")]

    [HttpPost("{id:guid}/match")]
    public async Task<IActionResult> MatchInvoice(Guid id, [FromBody] InvoiceActionDto dto)
    {
        var result = await _service.MatchInvoiceAsync(id, dto);
        return Ok(result);
    }

    [EnableRateLimiting("mutations")]

    [HttpPost("{id:guid}/pay")]
    public async Task<IActionResult> PayInvoice(Guid id, [FromBody] InvoicePayDto dto)
    {
        await _service.PayInvoiceAsync(id, dto);
        return NoContent();
    }

    [EnableRateLimiting("mutations")]

    [HttpPost("{id:guid}/void")]
    public async Task<IActionResult> VoidInvoice(Guid id, [FromBody] InvoiceRejectDto dto)
    {
        await _service.VoidInvoiceAsync(id, dto);
        return NoContent();
    }
}
