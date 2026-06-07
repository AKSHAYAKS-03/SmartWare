using System.Collections.Generic;
using System.Threading.Tasks;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Enums;
using System;

namespace SmartInventory.Core.Interfaces;

public interface IBarcodeService
{
    /// <summary>
    /// Generates a barcode or QR code as a byte array (PNG image).
    /// </summary>
    byte[] GenerateBarcode(string contents, BarcodeType type);

    /// <summary>
    /// Generates and persists a barcode record in the database.
    /// </summary>
    Task<BarcodeResponseDto> GenerateBarcodeRecordAsync(BarcodeGenerateRequestDto request);

    /// <summary>
    /// Generates and persists multiple barcode records in a single transaction.
    /// Maximum 500 records per call to prevent runaway requests.
    /// </summary>
    Task<IEnumerable<BarcodeResponseDto>> BatchGenerateBarcodeRecordsAsync(IEnumerable<BarcodeGenerateRequestDto> requests);

    /// <summary>
    /// Processes a barcode scan contextually and returns rich operational data.
    /// </summary>
    Task<ScanResultDto> ProcessScanAsync(BarcodeScanDto dto, Guid currentUserId, Guid? currentWarehouseId);

    /// <summary>
    /// Retrieves all barcode records associated with a product.
    /// </summary>
    Task<IEnumerable<BarcodeResponseDto>> GetProductBarcodesAsync(Guid productId);

    /// <summary>
    /// Retrieves a single barcode record by ID.
    /// </summary>
    Task<BarcodeResponseDto> GetBarcodeByIdAsync(Guid id);
}
