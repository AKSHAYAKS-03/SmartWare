using System.Threading.Tasks;
using SmartInventory.Core.Entities;

namespace SmartInventory.Core.Interfaces;
public interface IBarcodeRepository : IGenericRepository<Barcode>
{
    Task<Barcode?> GetByValueAsync(string barcodeValue);
}
