using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SmartInventory.Repository;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Search in local directory, or go up to the API project to find appsettings.json
        var basePath = Directory.GetCurrentDirectory();
        
        // If we are running tools from the Repository folder, the appsettings.json is in the API folder.
        if (!File.Exists(Path.Combine(basePath, "appsettings.json")))
        {
            var apiPath = Path.Combine(basePath, "..", "SmartInventory.API");
            if (Directory.Exists(apiPath) && File.Exists(Path.Combine(apiPath, "appsettings.json")))
            {
                basePath = apiPath;
            }
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // Fallback for default local database connection string in development
        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = "Host=localhost;Port=5432;Database=smart_inventory;Username=postgres;Password=12345";
        }

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString, b => b.MigrationsAssembly("SmartInventory.Repository"));

        return new AppDbContext(optionsBuilder.Options);
    }
}


// Find appsettings
// ↓
// Read Connection String
// ↓
// Configure PostgreSQL
// ↓
// Create AppDbContext
// ↓
// Return

// Compare Entity Models
// Generate Migration