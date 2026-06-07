using Microsoft.Extensions.Diagnostics.HealthChecks;
using SmartInventory.Repository;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SmartInventory.API.Services;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _context;

    public DatabaseHealthCheck(AppDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await _context.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Healthy("Database is online and responsive.");
            }
            return HealthCheckResult.Unhealthy("Database connection test failed.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database health check threw an exception.", ex);
        }
    }
}
