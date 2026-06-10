using ZXing;
using ZXing.Common;
using SmartInventory.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Service.Services;

public class BarcodeService : IBarcodeService
{
    private readonly IUnitOfWork _uow;

    public BarcodeService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    private static BarcodeResponseDto MapBarcodeResponse(Product product, Barcode barcode) => new()
    {
        Id = barcode.Id,
        ProductId = barcode.ProductId,
        ProductName = product.Name,
        ProductSKU = product.SKU,
        BarcodeValue = barcode.BarcodeValue,
        BarcodeType = barcode.BarcodeType,
        IsPrimary = barcode.IsPrimary,
        ImagePath = barcode.ImagePath,
        CreatedAt = barcode.CreatedAt
    };

    public byte[] GenerateBarcode(string contents, BarcodeType type)
    {
        if (string.IsNullOrWhiteSpace(contents))
            throw new ArgumentException("Contents cannot be empty.", nameof(contents));

        var format = type switch
        {
            BarcodeType.Code128 => BarcodeFormat.CODE_128,
            BarcodeType.QRCode => BarcodeFormat.QR_CODE,
            _ => BarcodeFormat.CODE_128
        };

        var width = type == BarcodeType.QRCode ? 250 : 300;
        var height = type == BarcodeType.QRCode ? 250 : 100;

        var writer = new BarcodeWriterPixelData
        {
            Format = format,
            Options = new EncodingOptions
            {
                Width = width,
                Height = height,
                Margin = 2,
                PureBarcode = type == BarcodeType.Code128 // Don't show human-readable text underneath inside the barcode raw pixels
            }
        };

        var pixelData = writer.Write(contents);

        // Convert the raw BGRA pixel bytes into a valid 32-bit BMP file byte array.
        // This is 100% portable across Windows, macOS, and Linux with zero external drawing dependencies.
        return ConvertToBmp(pixelData.Pixels, pixelData.Width, pixelData.Height);
    }

    private static byte[] ConvertToBmp(byte[] bgraPixels, int width, int height)
    {
        int fileHeaderSize = 14;
        int infoHeaderSize = 40;
        int pixelDataSize = bgraPixels.Length;
        int fileSize = fileHeaderSize + infoHeaderSize + pixelDataSize;

        byte[] bmp = new byte[fileSize];

        // --- File Header (14 bytes) ---
        bmp[0] = 0x42; // 'B'
        bmp[1] = 0x4D; // 'M'
        
        // File Size
        BitConverter.GetBytes(fileSize).CopyTo(bmp, 2);
        
        // Reserved (4 bytes) - 0
        
        // Offset to start of pixel data (54 bytes)
        BitConverter.GetBytes(fileHeaderSize + infoHeaderSize).CopyTo(bmp, 10);

        // --- Info Header (40 bytes) ---
        BitConverter.GetBytes(infoHeaderSize).CopyTo(bmp, 14); // Info Header Size
        BitConverter.GetBytes(width).CopyTo(bmp, 18);          // Width
        
        // Height is negative to denote top-down bitmap (otherwise BMP is bottom-up)
        BitConverter.GetBytes(-height).CopyTo(bmp, 22);        
        
        BitConverter.GetBytes((short)1).CopyTo(bmp, 26);       // Planes (must be 1)
        BitConverter.GetBytes((short)32).CopyTo(bmp, 28);      // Bits per Pixel (32-bit BGRA)
        BitConverter.GetBytes(0).CopyTo(bmp, 30);              // Compression (0 = BI_RGB, uncompressed)
        BitConverter.GetBytes(pixelDataSize).CopyTo(bmp, 34);  // Image Size
        BitConverter.GetBytes(2835).CopyTo(bmp, 38);           // X Pixels Per Meter (~72 DPI)
        BitConverter.GetBytes(2835).CopyTo(bmp, 42);           // Y Pixels Per Meter (~72 DPI)
        // Colors & Important Colors default to 0

        // Copy raw pixel data
        Array.Copy(bgraPixels, 0, bmp, fileHeaderSize + infoHeaderSize, pixelDataSize);

        return bmp;
    }

    public async Task<BarcodeResponseDto> GenerateBarcodeRecordAsync(BarcodeGenerateRequestDto request)
    {
        var product = await _uow.Repository<Product>().GetByIdAsync(request.ProductId);
        if (product == null)
            throw new NotFoundException("Product", request.ProductId);

        var barcodeExists = await _uow.Repository<Barcode>().Query()
            .AnyAsync(b => b.ProductId == request.ProductId);

        if (barcodeExists)
            throw new BarcodeAlreadyExistsException();

        var barcode = new Barcode
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            BarcodeValue = product.SKU,
            BarcodeType = request.Type,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<Barcode>().AddAsync(barcode);

        await _uow.CommitAsync();

        return MapBarcodeResponse(product, barcode);
    }

    public async Task<BarcodeResponseDto> UpdateBarcodeRecordAsync(Guid productId, BarcodeUpdateDto request)
    {
        var product = await _uow.Repository<Product>().GetByIdAsync(productId);
        if (product == null)
            throw new NotFoundException("Product", productId);

        var barcode = await _uow.Repository<Barcode>().Query()
            .FirstOrDefaultAsync(b => b.ProductId == productId);

        if (barcode == null)
            throw new NotFoundException("Barcode", productId);

        barcode.BarcodeValue = product.SKU;
        barcode.BarcodeType = request.Type;
        barcode.IsPrimary = true;
        _uow.Repository<Barcode>().Update(barcode);

        await _uow.CommitAsync();

        return MapBarcodeResponse(product, barcode);
    }

    public async Task<IEnumerable<BarcodeResponseDto>> BatchGenerateBarcodeRecordsAsync(
        IEnumerable<BarcodeGenerateRequestDto> requests)
    {
        var requestList = requests.ToList();
        if (requestList.Count == 0)
            throw new BusinessRuleException("Batch request must contain at least one item.");
        if (requestList.Count > 500)
            throw new BusinessRuleException("Batch request cannot exceed 500 items per call.");

        var productIds = requestList.Select(r => r.ProductId).ToList();
        if (productIds.Distinct().Count() != productIds.Count)
            throw new BusinessRuleException("Batch contains duplicate ProductIds within the request.");

        // Validate all product IDs exist
        productIds = productIds.Distinct().ToList();
        var products = await _uow.Repository<Product>().Query()
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();
        var missingIds = productIds.Except(products.Select(p => p.Id)).ToList();
        if (missingIds.Any())
            throw new NotFoundException("Product", missingIds.First());

        var productMap = products.ToDictionary(p => p.Id);
        var existingBarcodes = await _uow.Repository<Barcode>().Query()
            .Where(b => productIds.Contains(b.ProductId))
            .ToListAsync();

        if (existingBarcodes.Any())
            throw new BarcodeAlreadyExistsException();

        var barcodeMap = existingBarcodes
            .GroupBy(b => b.ProductId)
            .ToDictionary(g => g.Key, g => g.First());
        var results = new List<BarcodeResponseDto>();

        foreach (var request in requestList)
        {
            var product = productMap[request.ProductId];

            if (barcodeMap.TryGetValue(request.ProductId, out var barcode))
            {
                barcode.BarcodeValue = product.SKU;
                barcode.BarcodeType = request.Type;
                barcode.IsPrimary = true;
                _uow.Repository<Barcode>().Update(barcode);
            }
            else
            {
                barcode = new Barcode
                {
                    Id = Guid.NewGuid(),
                    ProductId = request.ProductId,
                    BarcodeValue = product.SKU,
                    BarcodeType = request.Type,
                    IsPrimary = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _uow.Repository<Barcode>().AddAsync(barcode);
                barcodeMap[request.ProductId] = barcode;
            }

            results.Add(MapBarcodeResponse(product, barcode));
        }

        // Single commit for the entire batch
        await _uow.CommitAsync();
        return results;
    }

    public async Task<ScanResultDto> ProcessScanAsync(BarcodeScanDto dto, Guid currentUserId, Guid? currentWarehouseId)
    {
        var barcode = await _uow.Repository<Barcode>().Query()
            .Include(b => b.Product)
            .FirstOrDefaultAsync(b => b.BarcodeValue == dto.BarcodeValue);

        if (barcode == null)
            throw new NotFoundException("Barcode", dto.BarcodeValue);

        // 1. Fetch all stock levels for this product
        var stockLevels = await _uow.Repository<StockLevel>().Query()
            .Include(s => s.BinLocation)
                .ThenInclude(b => b!.Zone)
                    .ThenInclude(z => z.Warehouse)
            .Where(s => s.ProductId == barcode.ProductId)
            .ToListAsync();

        // Optional: use warehouse context if provided by the authenticated user,
        // otherwise fall back to the warehouse passed in the scan payload.
        var effectiveWarehouseId = currentWarehouseId ?? dto.WarehouseId;
        if (effectiveWarehouseId != Guid.Empty)
        {
            stockLevels = stockLevels.Where(s => s.BinLocation != null && s.BinLocation.Zone.WarehouseId == effectiveWarehouseId).ToList();
        }

        // 2. Compute Totals
        var totalOnHand = stockLevels.Sum(s => s.QuantityOnHand);
        var totalReserved = stockLevels.Sum(s => s.QuantityReserved);

        // 3. Map Locations (only where bin location is assigned)
        var locations = stockLevels.Where(s => s.BinLocation != null).Select(s => new StockLocationDto
        {
            WarehouseId = s.BinLocation!.Zone.WarehouseId,
            WarehouseName = s.BinLocation.Zone.Warehouse.Name,
            ZoneId = s.BinLocation.ZoneId,
            ZoneName = s.BinLocation.Zone.Name,
            BinId = s.BinLocationId ?? Guid.Empty,
            BinCode = s.BinLocation.BinCode,
            QuantityOnHand = s.QuantityOnHand
        }).OrderByDescending(x => x.QuantityOnHand).ToList();

        // 4. Log the scan
        var actionName = dto.Action.ToString();
        var log = new BarcodeScanLog
        {
            Id = Guid.NewGuid(),
            BarcodeId = barcode.Id,
            ScannedBy = currentUserId,
            WarehouseId = effectiveWarehouseId,
            Action = dto.Action,
            ScannedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<BarcodeScanLog>().AddAsync(log);
        await _uow.CommitAsync();

        var warehouseName = locations.FirstOrDefault()?.WarehouseName ?? "Unknown";

        return new ScanResultDto
        {
            Message = "Scan processed successfully.",
            ProductId = barcode.ProductId,
            ProductName = barcode.Product.Name,
            ProductSKU = barcode.Product.SKU,
            CategoryName = "General", // Requires Category navigation property if real mapping is needed
            ProductType = "Standard",
            ABCClass = "Unknown",
            TotalOnHand = totalOnHand,
            TotalReserved = totalReserved,
            Locations = locations,
            ScanLogConfirmation = $"Scan logged: {DateTime.UtcNow:yyyy-MM-dd HH:mm tt} for Action: {actionName}"
        };
    }

    public async Task<BarcodeScanReceiptValidationResultDto> ValidateReceiptScanAsync(BarcodeScanReceiptValidationDto dto, Guid? currentWarehouseId)
    {
        var barcode = await _uow.Repository<Barcode>().Query()
            .Include(b => b.Product)
            .FirstOrDefaultAsync(b => b.BarcodeValue == dto.BarcodeValue);

        if (barcode == null)
            throw new NotFoundException("Barcode", dto.BarcodeValue);

        var effectiveWarehouseId = currentWarehouseId ?? dto.WarehouseId ?? Guid.Empty;
        if (effectiveWarehouseId == Guid.Empty)
            throw new BusinessRuleException("Warehouse context is required for receipt scan validation.");

        var result = new BarcodeScanReceiptValidationResultDto
        {
            IsValid = true,
            Message = "Barcode is valid for receipt scanning.",
            ProductId = barcode.ProductId,
            ProductName = barcode.Product?.Name ?? string.Empty,
            ProductSKU = barcode.Product?.SKU ?? string.Empty,
            PurchaseOrderId = dto.PurchaseOrderId,
            PurchaseOrderNumber = null,
            QuantityRemaining = 0,
            QuantityReceived = 0,
            QuantityOrdered = 0,
            WarehouseName = null,
            BinLocationId = null,
            BinCode = null
        };

        if (dto.PurchaseOrderId.HasValue)
        {
            var po = await _uow.Repository<PurchaseOrder>()
                .Query()
                .Include(p => p.Items)
                .Include(p => p.Shipments).ThenInclude(s => s.Items)
                .FirstOrDefaultAsync(p => p.Id == dto.PurchaseOrderId.Value);

            if (po == null)
                throw new NotFoundException("PurchaseOrder", dto.PurchaseOrderId.Value);

            var poItem = po.Items.FirstOrDefault(i => i.ProductId == barcode.ProductId);
            if (poItem == null)
                throw new BusinessRuleException($"Barcode does not match a product on Purchase Order {po.PoNumber}.");

            var remaining = poItem.QuantityOrdered - poItem.QuantityReceived;
            result.PurchaseOrderNumber = po.PoNumber;
            result.PurchaseOrderItemId = poItem.Id;
            result.QuantityOrdered = poItem.QuantityOrdered;
            result.QuantityReceived = poItem.QuantityReceived;
            result.QuantityRemaining = Math.Max(0, remaining);
        }

        if (!string.IsNullOrWhiteSpace(dto.BinBarcode))
        {
            var bin = await _uow.Repository<BinLocation>()
                .Query()
                .Include(b => b.Zone)
                .ThenInclude(z => z.Warehouse)
                .FirstOrDefaultAsync(b => b.Barcode == dto.BinBarcode && b.Zone.WarehouseId == effectiveWarehouseId);

            if (bin == null)
                throw new NotFoundException("BinLocation", dto.BinBarcode!);

            result.BinLocationId = bin.Id;
            result.BinCode = bin.Barcode ?? bin.BinCode;
            result.WarehouseName = bin.Zone.Warehouse.Name;
        }

        return result;
    }

    public async Task<BarcodeScanTransferValidationResultDto> ValidateTransferScanAsync(BarcodeScanTransferValidationDto dto, Guid? currentWarehouseId)
    {
        var barcode = await _uow.Repository<Barcode>().Query()
            .Include(b => b.Product)
            .FirstOrDefaultAsync(b => b.BarcodeValue == dto.BarcodeValue);

        if (barcode == null)
            throw new NotFoundException("Barcode", dto.BarcodeValue);

        var transfer = await _uow.Repository<WarehouseTransfer>()
            .Query()
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .Include(t => t.ToWarehouse)
            .Include(t => t.FromWarehouse)
            .FirstOrDefaultAsync(t => t.Id == dto.TransferId);

        if (transfer == null)
            throw new NotFoundException("WarehouseTransfer", dto.TransferId);

        var effectiveWarehouseId = currentWarehouseId ?? dto.WarehouseId;
        if (effectiveWarehouseId.HasValue && transfer.FromWarehouseId != effectiveWarehouseId.Value && transfer.ToWarehouseId != effectiveWarehouseId.Value)
            throw new UnauthorizedAccessException("You do not have access to this transfer.");

        var transferItem = transfer.Items.FirstOrDefault(i => i.ProductId == barcode.ProductId);
        if (transferItem == null)
            throw new BusinessRuleException("Scanned barcode does not belong to any product on this transfer.");

        Guid? binLocationId = null;
        string? binCode = null;
        if (!string.IsNullOrWhiteSpace(dto.BinBarcode))
        {
            var bin = await _uow.Repository<BinLocation>()
                .Query()
                .Include(b => b.Zone)
                .ThenInclude(z => z.Warehouse)
                .FirstOrDefaultAsync(b => b.Barcode == dto.BinBarcode && b.Zone.WarehouseId == transfer.ToWarehouseId);

            if (bin == null)
                throw new NotFoundException("BinLocation", dto.BinBarcode!);

            binLocationId = bin.Id;
            binCode = bin.Barcode ?? bin.BinCode;
        }

        return new BarcodeScanTransferValidationResultDto
        {
            IsValid = true,
            Message = "Transfer scan validated.",
            ProductId = barcode.ProductId,
            ProductName = barcode.Product?.Name ?? string.Empty,
            ProductSKU = barcode.Product?.SKU ?? string.Empty,
            TransferId = transfer.Id,
            TransferItemId = transferItem.Id,
            QuantityRequested = transferItem.QuantityRequested,
            QuantityDispatched = transferItem.QuantityDispatched,
            QuantityRemaining = Math.Max(0, transferItem.QuantityDispatched - transferItem.QuantityReceived),
            BinLocationId = binLocationId,
            BinCode = binCode,
            WarehouseName = transfer.ToWarehouse?.Name ?? transfer.FromWarehouse?.Name
        };
    }

    public async Task<IEnumerable<BarcodeResponseDto>> GetProductBarcodesAsync(Guid productId)
    {
        var product = await _uow.Repository<Product>().GetByIdAsync(productId);
        if (product == null)
            throw new NotFoundException("Product", productId);

        var list = await _uow.Repository<Barcode>().Query()
            .Where(b => b.ProductId == productId)
            .Select(b => new BarcodeResponseDto
            {
                Id = b.Id,
                ProductId = b.ProductId,
                ProductName = product.Name,
                ProductSKU = product.SKU,
                BarcodeValue = b.BarcodeValue,
                BarcodeType = b.BarcodeType,
                IsPrimary = b.IsPrimary,
                ImagePath = b.ImagePath,
                CreatedAt = b.CreatedAt
            })
            .ToListAsync();

        return list;
    }

    public async Task<BarcodeResponseDto> GetBarcodeByIdAsync(Guid id)
    {
        var barcode = await _uow.Repository<Barcode>().Query()
            .Include(b => b.Product)
            .FirstOrDefaultAsync(b => b.Id == id);
            
        if (barcode == null)
            throw new NotFoundException("Barcode", id);

        return new BarcodeResponseDto
        {
            Id = barcode.Id,
            ProductId = barcode.ProductId,
            ProductName = barcode.Product?.Name ?? string.Empty,
            ProductSKU = barcode.Product?.SKU ?? string.Empty,
            BarcodeValue = barcode.BarcodeValue,
            BarcodeType = barcode.BarcodeType,
            IsPrimary = barcode.IsPrimary,
            ImagePath = barcode.ImagePath,
            CreatedAt = barcode.CreatedAt
        };
    }
}
