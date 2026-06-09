using System.Collections.Generic;
using System.Threading.Tasks;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Enums;
using System;

namespace SmartInventory.Core.Interfaces;

public interface IBarcodeService
{
    byte[] GenerateBarcode(string contents, BarcodeType type);

    Task<BarcodeResponseDto> GenerateBarcodeRecordAsync(BarcodeGenerateRequestDto request);

    Task<BarcodeResponseDto> UpdateBarcodeRecordAsync(Guid productId, BarcodeUpdateDto request);

    Task<IEnumerable<BarcodeResponseDto>> BatchGenerateBarcodeRecordsAsync(IEnumerable<BarcodeGenerateRequestDto> requests);

    Task<ScanResultDto> ProcessScanAsync(BarcodeScanDto dto, Guid currentUserId, Guid? currentWarehouseId);

    Task<BarcodeScanReceiptValidationResultDto> ValidateReceiptScanAsync(BarcodeScanReceiptValidationDto dto, Guid? currentWarehouseId);
    Task<BarcodeScanTransferValidationResultDto> ValidateTransferScanAsync(BarcodeScanTransferValidationDto dto, Guid? currentWarehouseId);

    Task<IEnumerable<BarcodeResponseDto>> GetProductBarcodesAsync(Guid productId);

    Task<BarcodeResponseDto> GetBarcodeByIdAsync(Guid id);
}
