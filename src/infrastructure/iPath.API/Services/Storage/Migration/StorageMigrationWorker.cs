using iPath.Application.Contracts.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;

namespace iPath.API.Services.Storage.Migration;

/// <summary>Runs storage migrations one at a time; unfinished ones are resumed after a restart.</summary>
public sealed class StorageMigrationWorker(IServiceProvider sp, IStorageMigrationQueue queue, ILogger<StorageMigrationWorker> logger)
    : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<iPathDbContext>();
            var unfinished = await db.StorageMigrations
                .Where(m => m.Status == StorageMigrationStatus.Pending || m.Status == StorageMigrationStatus.Running)
                .OrderBy(m => m.CreatedOn)
                .Select(m => m.Id)
                .ToListAsync(cancellationToken);
            foreach (var id in unfinished)
                await queue.EnqueueAsync(id, cancellationToken);
            if (unfinished.Count > 0)
                logger.LogInformation("Resuming {Count} storage migrations", unfinished.Count);
        }
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var migrationId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = sp.CreateScope();
                await scope.ServiceProvider.GetRequiredService<StorageMigrationProcessor>().RunAsync(migrationId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Storage migration {MigrationId} stopped with an error; it resumes on the next start or retry", migrationId);
            }
        }
    }
}
