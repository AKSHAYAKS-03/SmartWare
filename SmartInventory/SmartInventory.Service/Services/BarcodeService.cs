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

        var existing = await _uow.Repository<Barcode>().Query()
            .AnyAsync(b => b.BarcodeValue == request.BarcodeValue);
        if (existing)
            throw new BusinessRuleException("Barcode value already exists.");

        if (request.IsPrimary)
        {
            var primaries = await _uow.Repository<Barcode>().Query()
                .Where(b => b.ProductId == request.ProductId && b.IsPrimary)
                .ToListAsync();
            foreach (var p in primaries)
            {
                p.IsPrimary = false;
                _uow.Repository<Barcode>().Update(p);
            }
        }

        var barcode = new Barcode
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            BarcodeValue = request.BarcodeValue,
            BarcodeType = request.Type,
            IsPrimary = request.IsPrimary,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<Barcode>().AddAsync(barcode);
        await _uow.CommitAsync();

        return new BarcodeResponseDto
        {
            Id = barcode.Id,
            ProductId = barcode.ProductId,
            ProductName = product.Name,
            ProductSKU = product.SKU,
            BarcodeValue = barcode.BarcodeValue,
            BarcodeType = barcode.BarcodeType,
            IsPrimary = barcode.IsPrimary,
            CreatedAt = barcode.CreatedAt
        };
    }

    public async Task<IEnumerable<BarcodeResponseDto>> BatchGenerateBarcodeRecordsAsync(
        IEnumerable<BarcodeGenerateRequestDto> requests)
    {
        var requestList = requests.ToList();
        if (requestList.Count == 0)
            throw new BusinessRuleException("Batch request must contain at least one item.");
        if (requestList.Count > 500)
            throw new BusinessRuleException("Batch request cannot exceed 500 items per call.");

        // Pre-check: all BarcodeValues must be unique within the batch
        var values = requestList.Select(r => r.BarcodeValue).ToList();
        if (values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Count)
            throw new BusinessRuleException("Batch contains duplicate BarcodeValues within the request.");

        // Pre-check: none of the BarcodeValues already exist in DB
        var existingValues = await _uow.Repository<Barcode>().Query()
            .Where(b => values.Contains(b.BarcodeValue))
            .Select(b => b.BarcodeValue)
            .ToListAsync();
        if (existingValues.Any())
            throw new BusinessRuleException(
                $"The following barcode values already exist: {string.Join(", ", existingValues)}");

        // Validate all product IDs exist
        var productIds = requestList.Select(r => r.ProductId).Distinct().ToList();
        var products = await _uow.Repository<Product>().Query()
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();
        var missingIds = productIds.Except(products.Select(p => p.Id)).ToList();
        if (missingIds.Any())
            throw new NotFoundException("Product", missingIds.First());

        var productMap = products.ToDictionary(p => p.Id);
        var results = new List<BarcodeResponseDto>();

        foreach (var request in requestList)
        {
            var product = productMap[request.ProductId];

            // If this is flagged as primary, demote all existing primaries for this product
            if (request.IsPrimary)
            {
                var primaries = await _uow.Repository<Barcode>().Query()
                    .Where(b => b.ProductId == request.ProductId && b.IsPrimary)
                    .ToListAsync();
                foreach (var p in primaries)
                {
                    p.IsPrimary = false;
                    _uow.Repository<Barcode>().Update(p);
                }
            }

            var barcode = new Barcode
            {
                Id = Guid.NewGuid(),
                ProductId = request.ProductId,
                BarcodeValue = request.BarcodeValue,
                BarcodeType = request.Type,
                IsPrimary = request.IsPrimary,
                CreatedAt = DateTime.UtcNow
            };

            await _uow.Repository<Barcode>().AddAsync(barcode);
            results.Add(new BarcodeResponseDto
            {
                Id = barcode.Id,
                ProductId = barcode.ProductId,
                ProductName = product.Name,
                ProductSKU = product.SKU,
                BarcodeValue = barcode.BarcodeValue,
                BarcodeType = barcode.BarcodeType,
                IsPrimary = barcode.IsPrimary,
                CreatedAt = barcode.CreatedAt
            });
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

        // Optional: Filter by warehouse context if provided
        if (currentWarehouseId.HasValue)
        {
            stockLevels = stockLevels.Where(s => s.BinLocation != null && s.BinLocation.Zone.WarehouseId == currentWarehouseId.Value).ToList();
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
            WarehouseId = currentWarehouseId ?? Guid.Empty, // Or handle appropriately
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
