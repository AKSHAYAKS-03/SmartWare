using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;
using System;
using System.Threading.Tasks;

namespace SmartInventory.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class BarcodesController : ControllerBase
{
    private readonly IBarcodeService _barcodeService;
    private readonly ICurrentUserService _currentUser;

    public BarcodesController(IBarcodeService barcodeService, ICurrentUserService currentUser)
    {
        _barcodeService = barcodeService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Ensures a product barcode exists using the product's generated SKU as the barcode value.
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost("generate")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> GenerateBarcode([FromBody] BarcodeGenerateRequestDto request)
    {
        var result = await _barcodeService.GenerateBarcodeRecordAsync(request);
        return Ok(result);
    }

    [EnableRateLimiting("mutations")]
    [HttpPut("product/{productId:guid}")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> UpdateBarcode(Guid productId, [FromBody] BarcodeUpdateDto request)
    {
        var result = await _barcodeService.UpdateBarcodeRecordAsync(productId, request);
        return Ok(result);
    }

    /// <summary>
    /// Ensures multiple product barcodes exist in a single transaction (max 500).
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost("batch-generate")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> BatchGenerateBarcodes([FromBody] List<BarcodeGenerateRequestDto> requests)
    {
        var results = await _barcodeService.BatchGenerateBarcodeRecordsAsync(requests);
        return Ok(results);
    }

    /// <summary>
    /// Processes a scan audit event for a scanned barcode, returning rich operational context.
    /// Restricted to operational roles only.
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost("scan")]
    [Authorize(Policy = "RequireStaff")]
    public async Task<IActionResult> ScanBarcode([FromBody] BarcodeScanDto dto)
    {
        var result = await _barcodeService.ProcessScanAsync(dto, _currentUser.UserId, _currentUser.WarehouseId);
        return Ok(result);
    }

    [EnableRateLimiting("mutations")]
    [HttpPost("scan/receipt/validate")]
    [Authorize(Policy = "RequireStaff")]
    public async Task<IActionResult> ValidateReceiptScan([FromBody] BarcodeScanReceiptValidationDto dto)
    {
        var result = await _barcodeService.ValidateReceiptScanAsync(dto, _currentUser.WarehouseId);
        return Ok(result);
    }

    [EnableRateLimiting("mutations")]
    [HttpPost("scan/transfer/validate")]
    [Authorize(Policy = "RequireStaff")]
    public async Task<IActionResult> ValidateTransferScan([FromBody] BarcodeScanTransferValidationDto dto)
    {
        var result = await _barcodeService.ValidateTransferScanAsync(dto, _currentUser.WarehouseId);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all barcode records associated with a product.
    /// </summary>
    [HttpGet("product/{productId:guid}")]
    public async Task<IActionResult> GetProductBarcodes(Guid productId)
    {
        var list = await _barcodeService.GetProductBarcodesAsync(productId);
        return Ok(list);
    }

    /// <summary>
    /// Generates and streams the raw barcode image file (BMP) directly.
    /// Requires authentication — barcode data is commercially sensitive product catalog information.
    /// </summary>
    [HttpGet("{id:guid}/image")]
    public async Task<IActionResult> GetBarcodeImage(Guid id)
    {
        var barcode = await _barcodeService.GetBarcodeByIdAsync(id);
        
        try
        {
            var imageBytes = _barcodeService.GenerateBarcode(barcode.BarcodeValue, barcode.BarcodeType);
            return File(imageBytes, "image/bmp");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to generate barcode image.", details = ex.Message });
        }
    }
}
