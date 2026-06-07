using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartInventory.Core.Entities;
using SmartInventory.Repository;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartInventory.Infrastructure.BackgroundJobs;

/// <summary>
/// Nightly background service that archives audit log records older than 365 days.
///
/// Business rationale:
///   — The hot audit_logs table must remain small for query performance.
///   — Regulatory requirements mandate long-term retention (7 years+).
///   — Records are MOVED (not deleted) to audit_log_archives — still queryable by compliance teams.
///
/// Schedule: Runs daily at 02:00 UTC.
/// Batch size: 1000 records per iteration to avoid large transactions.
/// </summary>
public class AuditLogArchiveJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogArchiveJob> _logger;

    private static readonly TimeSpan ArchiveThreshold = TimeSpan.FromDays(365);
    private const int BatchSize = 1000;

    public AuditLogArchiveJob(IServiceScopeFactory scopeFactory, ILogger<AuditLogArchiveJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AuditLogArchiveJob started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Calculate delay until next 02:00 UTC
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1).AddHours(2); // Tomorrow at 02:00 UTC
            if (now.TimeOfDay < TimeSpan.FromHours(2))
            {
                // It's before 02:00 today — run today
                nextRun = now.Date.AddHours(2);
            }

            var delay = nextRun - now;
            _logger.LogInformation("AuditLogArchiveJob will run at {NextRun} UTC (in {Delay:hh\\:mm} hours).",
                nextRun, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break; // Application is shutting down
            }

            await RunArchiveCycleAsync(stoppingToken);
        }

        _logger.LogInformation("AuditLogArchiveJob stopped.");
    }

    /// <summary>
    /// Performs a single archive cycle: batch-moves old audit logs to cold storage.
    /// </summary>
    private async Task RunArchiveCycleAsync(CancellationToken stoppingToken)
    {
        var cutoff = DateTime.UtcNow - ArchiveThreshold;
        int totalArchived = 0;
        var cycleId = Guid.NewGuid();

        using var logScope = _logger.BeginScope(new System.Collections.Generic.Dictionary<string, object> { ["ArchiveCycleId"] = cycleId });
        _logger.LogInformation("AuditLogArchiveJob: Starting archive cycle. Cutoff date: {Cutoff}", cutoff);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        bool hasMore = true;
        while (hasMore && !stoppingToken.IsCancellationRequested)
        {
            // Fetch a batch of old audit logs
            var batch = await db.AuditLogs
                .Where(a => a.CreatedAt < cutoff)
                .OrderBy(a => a.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(stoppingToken);

            if (!batch.Any())
            {
                hasMore = false;
                break;
            }

            // Map to archive records
            var archiveRecords = batch.Select(a => new AuditLogArchive
            {
                Id = Guid.NewGuid(),
                UserId = a.UserId,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Action = a.Action,
                OldValues = a.OldValues,
                NewValues = a.NewValues,
                IpAddress = a.IpAddress,
                OriginalCreatedAt = a.CreatedAt,
                ArchivedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            // Insert archives first, then delete originals — safe even if interrupted mid-batch
            await db.AuditLogArchives.AddRangeAsync(archiveRecords, stoppingToken);
            await db.SaveChangesAsync(stoppingToken);

            db.AuditLogs.RemoveRange(batch);
            await db.SaveChangesAsync(stoppingToken);

            totalArchived += batch.Count;
            hasMore = batch.Count == BatchSize; // If full batch, there may be more

            _logger.LogInformation(
                "AuditLogArchiveJob: Archived {BatchCount} records. Total this cycle: {TotalArchived}.",
                batch.Count, totalArchived);
        }

        sw.Stop();
        _logger.LogInformation(
            "AuditLogArchiveJob: Cycle complete. Total records archived: {TotalArchived}. Elapsed time: {ElapsedMilliseconds}ms.", 
            totalArchived, sw.ElapsedMilliseconds);
    }
}
