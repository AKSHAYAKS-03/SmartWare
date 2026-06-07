using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mapster;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Service.Services;

/// <summary>
/// Product catalog service.
///
/// Business rules enforced:
///   — SKU must be unique across all active products.
///   — Soft delete is blocked when the product has active stock (quantity > 0).
///   — Barcode generation is triggered automatically on product creation.
///   — Manager and Staff roles see only products with stock in their assigned warehouse.
///   — ABC category is persisted after running the classification engine.
/// </summary>
public class ProductService : IProductService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IBarcodeService _barcodeService;
    private readonly ICacheService _cacheService;

    public ProductService(IUnitOfWork uow, ICurrentUserService currentUser, IBarcodeService barcodeService, ICacheService cacheService)
    {
        _uow = uow;
        _currentUser = currentUser;
        _barcodeService = barcodeService;
        _cacheService = cacheService;
    }

    public async Task<ProductResponseDto> CreateProductAsync(ProductCreateDto dto)
    {
        // 1. Validate unique SKU
        bool skuExists = await _uow.Repository<Product>()
            .Query().AnyAsync(p => p.SKU == dto.SKU);
        if (skuExists)
            throw new BusinessRuleException($"A product with SKU '{dto.SKU}' already exists.");

        // 2. Validate category
        var category = await _uow.Repository<Category>().GetByIdAsync(dto.CategoryId);
        if (category == null)
            throw new NotFoundException("Category", dto.CategoryId);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            SKU = dto.SKU,
            Description = dto.Description,
            UnitOfMeasure = dto.UnitOfMeasure,
            CostPrice = dto.CostPrice,
            SellingPrice = dto.SellingPrice,
            ReorderPoint = dto.ReorderPoint,
            ReorderQuantity = dto.ReorderQuantity,
            CategoryId = dto.CategoryId,
            IsActive = dto.IsActive,
            ImagePath = dto.ImagePath,
            Length = dto.Length,
            Width = dto.Width,
            Height = dto.Height,
            WeightKg = dto.WeightKg,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<Product>().AddAsync(product);
        await _uow.CommitAsync();

        // 3. Auto-generate primary barcode for the new product (using SKU)
        var barcode = new Barcode
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            BarcodeValue = product.SKU,
            BarcodeType = BarcodeType.Code128,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow
        };
        await _uow.Repository<Barcode>().AddAsync(barcode);
        await _uow.CommitAsync();

        return await GetProductByIdAsync(product.Id);
    }

    public async Task<ProductResponseDto> UpdateProductAsync(Guid productId, ProductUpdateDto dto)
    {
        var product = await _uow.Repository<Product>().GetByIdAsync(productId);
        if (product == null)
            throw new NotFoundException("Product", productId);

        var category = await _uow.Repository<Category>().GetByIdAsync(dto.CategoryId);
        if (category == null)
            throw new NotFoundException("Category", dto.CategoryId);

        bool hasDimensionsChanged = product.Length != dto.Length ||
                                    product.Width != dto.Width ||
                                    product.Height != dto.Height ||
                                    product.WeightKg != dto.WeightKg;

        if (hasDimensionsChanged)
        {
            // Calculate actual physical stock quantity
            int totalQoH = await _uow.Repository<StockLevel>()
                .Query()
                .Where(sl => sl.ProductId == productId)
                .SumAsync(sl => sl.QuantityOnHand);

            if (totalQoH > 0)
            {
                throw new BusinessRuleException($"Cannot modify Length, Width, Height, or WeightKg because active stock exists (Quantity: {totalQoH}). Adjust stock to zero before changing product dimensions to prevent capacity drift.");
            }

            product.Length = dto.Length;
            product.Width = dto.Width;
            product.Height = dto.Height;
            product.WeightKg = dto.WeightKg;
        }

        product.Name = dto.Name;
        product.Description = dto.Description;
        product.UnitOfMeasure = dto.UnitOfMeasure;
        product.CostPrice = dto.CostPrice;
        product.SellingPrice = dto.SellingPrice;
        product.ReorderPoint = dto.ReorderPoint;
        product.ReorderQuantity = dto.ReorderQuantity;
        product.CategoryId = dto.CategoryId;
        product.IsActive = dto.IsActive;
        product.ImagePath = dto.ImagePath;

        _uow.Repository<Product>().Update(product);
        await _uow.CommitAsync();
        await _cacheService.RemoveAsync($"product:id:{productId}");

        return await GetProductByIdAsync(productId);
    }

    public async Task DeleteProductAsync(Guid productId)
    {
        var product = await _uow.Repository<Product>().GetByIdAsync(productId);
        if (product == null)
            throw new NotFoundException("Product", productId);

        // Block deletion if active stock exists
        bool hasStock = await _uow.Repository<StockLevel>()
            .Query()
            .AnyAsync(sl => sl.ProductId == productId && sl.QuantityOnHand > 0);

        if (hasStock)
            throw new BusinessRuleException(
                "Cannot delete a product with existing stock. Adjust stock to zero first.");

        _uow.Repository<Product>().Delete(product); // Soft delete via ISoftDelete interceptor
        await _uow.CommitAsync();
        await _cacheService.RemoveAsync($"product:id:{productId}");
    }

    public async Task<ProductResponseDto> GetProductByIdAsync(Guid productId)
    {
        var cacheKey = $"product:id:{productId}";
        var cached = await _cacheService.GetAsync<ProductResponseDto>(cacheKey);
        if (cached != null)
        {
            return cached;
        }

        var product = await _uow.Repository<Product>()
            .Query()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == productId);

        if (product == null)
            throw new NotFoundException("Product", productId);

        var dto = product.Adapt<ProductResponseDto>();
        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5));
        return dto;
    }

    public async Task<PagedResult<ProductResponseDto>> GetProductsAsync(ProductQueryParameters queryParams)
    {
        IQueryable<Product> query = _uow.Repository<Product>().Query().Include(p => p.Category);

        // Scoped access: if the user has a warehouse scope claim (Manager, Staff, or Scoped Viewer), they see only products with stock in their warehouse
        if (_currentUser.WarehouseId.HasValue)
        {
            var warehouseId = _currentUser.WarehouseId.Value;
            var productIdsInWarehouse = await _uow.Repository<StockLevel>()
                .Query()
                .Where(sl => sl.WarehouseId == warehouseId)
                .Select(sl => sl.ProductId)
                .Distinct()
                .ToListAsync();

            query = query.Where(p => productIdsInWarehouse.Contains(p.Id));
        }
        // Explicit warehouse filter (for Unscoped Admin/Viewer who pass it as a query param)
        else if (queryParams.WarehouseId.HasValue)
        {
            var wId = queryParams.WarehouseId.Value;
            var productIdsInWarehouse = await _uow.Repository<StockLevel>()
                .Query()
                .Where(sl => sl.WarehouseId == wId)
                .Select(sl => sl.ProductId)
                .Distinct()
                .ToListAsync();
            query = query.Where(p => productIdsInWarehouse.Contains(p.Id));
        }

        if (queryParams.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == queryParams.CategoryId.Value);

        if (queryParams.IsActive.HasValue)
            query = query.Where(p => p.IsActive == queryParams.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
            query = query.Where(p => p.Name.Contains(queryParams.Search) ||
                                     p.SKU.Contains(queryParams.Search));

        if (queryParams.LowStockOnly == true)
        {
            // Only products where total QoH <= reorder point
            query = query.Where(p =>
                p.StockLevels.Sum(sl => sl.QuantityOnHand) <= p.ReorderPoint);
        }

        int totalCount = await query.CountAsync();

        query = queryParams.SortDir.ToLower() == "asc"
            ? query.OrderBy(p => p.Name)
            : query.OrderByDescending(p => p.CreatedAt);

        var data = await query
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync();

        return new PagedResult<ProductResponseDto>
        {
            Data = data.Adapt<IEnumerable<ProductResponseDto>>(),
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    public async Task<PagedResult<ProductResponseDto>> SearchProductsAsync(DynamicQueryRequest request)
    {
        // Execute dynamic search. Include Category to ensure DTO mapping has all required navigation properties.
        var pagedResult = await _uow.Repository<Product>()
            .GetPagedDynamicAsync(request, p => p.Category);

        return new PagedResult<ProductResponseDto>
        {
            Data = pagedResult.Data.Adapt<IEnumerable<ProductResponseDto>>(),
            TotalCount = pagedResult.TotalCount,
            Page = pagedResult.Page,
            PageSize = pagedResult.PageSize
        };
    }

    public async Task<IEnumerable<ProductResponseDto>> GetLowStockProductsAsync(Guid? warehouseId = null)
    {
        var stockQuery = _uow.Repository<StockLevel>().Query();
        if (warehouseId.HasValue)
            stockQuery = stockQuery.Where(sl => sl.WarehouseId == warehouseId.Value);

        // Get product IDs where total QoH <= reorder point
        var lowStockProductIds = await stockQuery
            .GroupBy(sl => new { sl.ProductId, sl.Product.ReorderPoint })
            .Where(g => g.Sum(sl => sl.QuantityOnHand) <= g.Key.ReorderPoint)
            .Select(g => g.Key.ProductId)
            .ToListAsync();

        var products = await _uow.Repository<Product>()
            .Query()
            .Include(p => p.Category)
            .Where(p => lowStockProductIds.Contains(p.Id))
            .ToListAsync();

        return products.Adapt<IEnumerable<ProductResponseDto>>();
    }

    public async Task<IEnumerable<ProductResponseDto>> GetDeadStockProductsAsync(int daysThreshold = 90)
    {
        var cutoff = DateTime.UtcNow.AddDays(-daysThreshold);

        // Get product IDs that had outbound movement after the cutoff (still active)
        var activeProductIds = await _uow.Repository<StockMovement>()
            .Query()
            .Where(m => m.CreatedAt >= cutoff &&
                        (m.MovementType == MovementType.Sale ||
                         m.MovementType == MovementType.TransferOut))
            .Select(m => m.ProductId)
            .Distinct()
            .ToListAsync();

        // Dead stock = products with stock on hand but no recent outbound movement
        var deadProducts = await _uow.Repository<Product>()
            .Query()
            .Include(p => p.Category)
            .Where(p => !activeProductIds.Contains(p.Id) &&
                        p.StockLevels.Any(sl => sl.QuantityOnHand > 0))
            .ToListAsync();

        return deadProducts.Adapt<IEnumerable<ProductResponseDto>>();
    }

    /// <summary>
    /// Runs the ABC classification engine and persists the category (A/B/C) to each product row.
    /// Called on-demand or by a scheduled job. Scoped to a specific warehouse.
    /// </summary>
    public async Task UpdateAbcCategoriesAsync(Guid warehouseId)
    {
        var stockLevels = await _uow.Repository<StockLevel>()
            .Query()
            .Include(sl => sl.Product)
            .Where(sl => sl.WarehouseId == warehouseId)
            .ToListAsync();

        if (!stockLevels.Any()) return;

        var groups = stockLevels
            .GroupBy(sl => sl.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Product = g.First().Product,
                TotalValue = g.Sum(sl => sl.QuantityOnHand) * g.First().Product.SellingPrice
            })
            .OrderByDescending(g => g.TotalValue)
            .ToList();

        decimal totalValuation = groups.Sum(g => g.TotalValue);
        if (totalValuation == 0) return;

        decimal running = 0;
        foreach (var item in groups)
        {
            running += item.TotalValue;
            double pct = (double)(running / totalValuation) * 100.0;

            item.Product.AbcCategory = pct <= 70.0 ? "A" : pct <= 90.0 ? "B" : "C";
            _uow.Repository<Product>().Update(item.Product);
        }

        await _uow.CommitAsync();
    }

}
