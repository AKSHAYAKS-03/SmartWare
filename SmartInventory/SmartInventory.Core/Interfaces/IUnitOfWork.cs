using System;
using System.Threading.Tasks;
using SmartInventory.Core.Entities;

namespace SmartInventory.Core.Interfaces;

// Unit of Work interface to manage database transactions atomically across multiple repositories.

public interface IUnitOfWork : IDisposable
{
    IProductRepository Products { get; }
    ISupplierRepository Suppliers { get; }
    IPurchaseOrderRepository PurchaseOrders { get; }
    ITransferRepository Transfers { get; }
    IBarcodeRepository Barcodes { get; }
    INotificationRepository Notifications { get; }
    IStockLevelRepository StockLevels { get; }

    
    IGenericRepository<T> Repository<T>() where T : BaseEntity;
    Task<int> CommitAsync();
}
