using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Repository.Repositories;


public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly ConcurrentDictionary<string, object> _dynamicRepositories; //stores generic repositories

    public IProductRepository Products { get; }
    public ISupplierRepository Suppliers { get; }
    public IPurchaseOrderRepository PurchaseOrders { get; }
    public ITransferRepository Transfers { get; }
    public IBarcodeRepository Barcodes { get; }
    public INotificationRepository Notifications { get; }
    public IStockLevelRepository StockLevels { get; }

    public UnitOfWork(
        AppDbContext context,
        IProductRepository products,
        ISupplierRepository suppliers,
        IPurchaseOrderRepository purchaseOrders,
        ITransferRepository transfers,
        IBarcodeRepository barcodes,
        INotificationRepository notifications,
        IStockLevelRepository stockLevels)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dynamicRepositories = new ConcurrentDictionary<string, object>();

        Products = products ?? throw new ArgumentNullException(nameof(products));
        Suppliers = suppliers ?? throw new ArgumentNullException(nameof(suppliers));
        PurchaseOrders = purchaseOrders ?? throw new ArgumentNullException(nameof(purchaseOrders));
        Transfers = transfers ?? throw new ArgumentNullException(nameof(transfers));
        Barcodes = barcodes ?? throw new ArgumentNullException(nameof(barcodes));
        Notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        StockLevels = stockLevels ?? throw new ArgumentNullException(nameof(stockLevels));
    }

    // Get generic repository for any entity
    public IGenericRepository<T> Repository<T>() where T : BaseEntity
    {
        var typeName = typeof(T).Name;

        return (IGenericRepository<T>)_dynamicRepositories.GetOrAdd(typeName, _ => new GenericRepository<T>(_context));
    }

    public async Task<int> CommitAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this); //garbage collector
    }
}
