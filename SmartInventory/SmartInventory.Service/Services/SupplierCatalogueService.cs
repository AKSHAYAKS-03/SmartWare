using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Service.Services;

/// <summary>
/// Manages the supplier's product catalogue within the portal.
/// SECURITY: All reads and mutations filter on supplierId from JWT — suppliers
/// cannot see or edit other suppliers' prices or catalogue entries.
/// </summary>
public class SupplierCatalogueService : ISupplierCatalogueService
{
    private readonly IUnitOfWork _uow;

    public SupplierCatalogueService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET MY CATALOGUE
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<SupplierCatalogueItemDto>> GetMyCatalogueAsync(Guid supplierId)
    {
        var items = await _uow.Repository<SupplierProduct>().Query()
            .Include(sp => sp.Product).ThenInclude(p => p.Category)
            .Where(sp => sp.SupplierId == supplierId)
            .OrderBy(sp => sp.Product.Name)
            .ToListAsync();

        return items.Select(sp => MapToDto(sp)).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UPDATE CATALOGUE ITEM
    // ─────────────────────────────────────────────────────────────────────────

    public async Task UpdateCatalogueItemAsync(Guid supplierId, Guid supplierProductId, SupplierUpdateCatalogueItemRequest request)
    {
        var sp = await GetSupplierProductOrThrowAsync(supplierId, supplierProductId);

        sp.UnitPrice = request.UnitPrice;
        sp.LeadTimeDays = request.LeadTimeDays;
        sp.MinOrderQuantity = request.MinOrderQuantity;

        _uow.Repository<SupplierProduct>().Update(sp);
        await _uow.CommitAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ADD CATALOGUE ITEM
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<SupplierCatalogueItemDto> AddCatalogueItemAsync(Guid supplierId, SupplierAddCatalogueItemRequest request)
    {
        // Ensure the product exists
        var product = await _uow.Repository<Product>().Query()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.IsActive);

        if (product == null)
            throw new NotFoundException("Product", request.ProductId);

        // Check for duplicate
        var existing = await _uow.Repository<SupplierProduct>().Query()
            .AnyAsync(sp => sp.SupplierId == supplierId && sp.ProductId == request.ProductId);

        if (existing)
            throw new BusinessRuleException("This product is already in your catalogue. Use the update endpoint to change pricing.");

        var newItem = new SupplierProduct
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            ProductId = request.ProductId,
            UnitPrice = request.UnitPrice,
            LeadTimeDays = request.LeadTimeDays,
            MinOrderQuantity = request.MinOrderQuantity,
            IsPreferred = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<SupplierProduct>().AddAsync(newItem);
        await _uow.CommitAsync();

        // Reload with navigation for the response
        newItem.Product = product;
        return MapToDto(newItem);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DEACTIVATE CATALOGUE ITEM
    // ─────────────────────────────────────────────────────────────────────────

    public async Task DeactivateCatalogueItemAsync(Guid supplierId, Guid supplierProductId)
    {
        var sp = await GetSupplierProductOrThrowAsync(supplierId, supplierProductId);

        if (!sp.IsActive)
            throw new BusinessRuleException("This catalogue item is already inactive.");

        sp.IsActive = false;
        _uow.Repository<SupplierProduct>().Update(sp);
        await _uow.CommitAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<SupplierProduct> GetSupplierProductOrThrowAsync(Guid supplierId, Guid supplierProductId)
    {
        var sp = await _uow.Repository<SupplierProduct>().Query()
            .Include(s => s.Product).ThenInclude(p => p.Category)
            .FirstOrDefaultAsync(s => s.Id == supplierProductId && s.SupplierId == supplierId);

        if (sp == null)
            throw new NotFoundException("SupplierProduct", supplierProductId);

        return sp;
    }

    private static SupplierCatalogueItemDto MapToDto(SupplierProduct sp)
    {
        return new SupplierCatalogueItemDto(
            SupplierProductId: sp.Id,
            ProductId: sp.ProductId,
            ProductName: sp.Product?.Name ?? string.Empty,
            Sku: sp.Product?.SKU ?? string.Empty,
            Category: sp.Product?.Category?.Name ?? string.Empty,
            UnitPrice: sp.UnitPrice,
            LeadTimeDays: sp.LeadTimeDays,
            MinOrderQuantity: sp.MinOrderQuantity,
            IsPreferred: sp.IsPreferred,
            IsActive: sp.IsActive
        );
    }
}
