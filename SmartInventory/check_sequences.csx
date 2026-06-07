using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartInventory.Repository;

var config = new ConfigurationBuilder()
    .SetBasePath("/Users/akshaya/SmartWare/SmartInventory/SmartInventory.API")
    .AddJsonFile("appsettings.json")
    .Build();

var connectionString = config.GetConnectionString("DefaultConnection");

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(connectionString)
    .Options;

using var db = new AppDbContext(options);

Console.WriteLine("--- sequence_counters ---");
foreach (var counter in db.Set<SmartInventory.Core.Entities.SequenceCounter>())
{
    Console.WriteLine($"{counter.EntityName}: {counter.Prefix}-{counter.CurrentValue}");
}

Console.WriteLine("--- Max values ---");
Console.WriteLine($"PO: " + db.Set<SmartInventory.Core.Entities.PurchaseOrder>().Count());
Console.WriteLine($"GRN: " + db.Set<SmartInventory.Core.Entities.GoodsReceipt>().Count());
Console.WriteLine($"TRF: " + db.Set<SmartInventory.Core.Entities.WarehouseTransfer>().Count());
Console.WriteLine($"ADJ: " + db.Set<SmartInventory.Core.Entities.StockAdjustment>().Count());

