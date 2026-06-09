using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SmartInventory.Core.Entities;
using SmartInventory.Repository;
using SmartInventory.Repository.Repositories;

namespace SmartInventory.Tests;

public class TransferStockLevelStateTransitionTests
{
    [Fact]
    public async Task AddAsyncThenUpdate_ChangesTrackedState_FromAddedToModified()
    {
        await using var context = TestDbContextFactory.Create();
        var repository = new GenericRepository<StockLevel>(context);

        var stockLevel = new StockLevel
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            WarehouseId = Guid.NewGuid(),
            BinLocationId = null,
            QuantityOnHand = 0,
            QuantityReserved = 0,
            QuantityOnOrder = 0,
            QuantityInTransit = 5,
            LastUpdated = DateTime.UtcNow
        };

        await repository.AddAsync(stockLevel);

        Assert.Equal(EntityState.Added, context.Entry(stockLevel).State);

        repository.Update(stockLevel);

        var entry = context.Entry(stockLevel);
        Assert.Equal(EntityState.Modified, entry.State);

        Assert.All(
            entry.Properties.Where(p => !p.Metadata.IsPrimaryKey()),
            property => Assert.True(property.IsModified));
    }

    [Fact]
    public void NpgsqlModel_Configures_StockLevel_Xmin_AsConcurrencyToken()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=smart_inventory;Username=postgres;Password=12345")
            .Options;

        using var context = new AppDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(StockLevel));
        Assert.NotNull(entityType);

        var xmin = entityType!.FindProperty("xmin");
        Assert.NotNull(xmin);
        Assert.True(xmin!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, xmin.ValueGenerated);
    }
}
