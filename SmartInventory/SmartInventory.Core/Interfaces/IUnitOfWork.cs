using System;
using System.Threading.Tasks;
using SmartInventory.Core.Entities;

namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Unit of Work interface to manage database transactions atomically across multiple repositories.
/// Designed by the senior developer to guarantee ACID compliance for logistical workflows.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IProductRepository Products { get; }
    ISupplierRepository Suppliers { get; }
    IPurchaseOrderRepository PurchaseOrders { get; }
    ITransferRepository Transfers { get; }
    IBarcodeRepository Barcodes { get; }
    INotificationRepository Notifications { get; }
    IStockLevelRepository StockLevels { get; }

    /// <summary>
    /// Dynamic repository resolver for entities that do not require specialized query behaviors.
    /// </summary>
    IGenericRepository<T> Repository<T>() where T : BaseEntity;

    /// <summary>
    /// Commits all modified entities tracked in the context as an atomic transaction.
    /// </summary>
    /// <returns>Number of state entries written to the database.</returns>
    Task<int> CommitAsync();
}
