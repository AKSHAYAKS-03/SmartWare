using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Repository.Repositories;

/// <summary>
/// Specialized Barcode repository implementing rapid scan value lookups.
/// </summary>
public class BarcodeRepository : GenericRepository<Barcode>, IBarcodeRepository
{
    public BarcodeRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Barcode?> GetByValueAsync(string barcodeValue)
    {
        if (string.IsNullOrWhiteSpace(barcodeValue))
            return null;

        // Perform rapid lookup including target Product details
        return await _dbSet
            .Include(b => b.Product)
            .FirstOrDefaultAsync(b => b.BarcodeValue == barcodeValue.Trim());
    }
}
