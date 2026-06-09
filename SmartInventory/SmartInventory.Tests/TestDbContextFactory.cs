using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartInventory.Repository;

namespace SmartInventory.Tests;

/// <summary>
/// Creates isolated in-memory AppDbContext instances for unit tests.
/// Avoids SQLite parsing errors from PostgreSQL-specific column defaults.
/// </summary>
public static class TestDbContextFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<TestAppDbContext>()
            .UseInMemoryDatabase($"SmartInventoryTests_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new TestAppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
