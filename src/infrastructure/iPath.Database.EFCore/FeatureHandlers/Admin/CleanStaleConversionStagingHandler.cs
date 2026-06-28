using iPath.Application.Features.Admin;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DispatchR.Abstractions.Send;

namespace iPath.EF.Core.FeatureHandlers.Admin;

public class CleanStaleConversionStagingHandler(
    iPathDbContext db,
    ILogger<CleanStaleConversionStagingHandler> logger)
    : IRequestHandler<CleanStaleConversionStagingCommand, Task<int>>
{
    public async Task<int> Handle(CleanStaleConversionStagingCommand request, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-request.DaysOld);
        
        var staleJobs = await db.Set<WsiConversionJob>()
            .Where(j => 
                (j.Status == WsiConversionStatus.Completed && j.CompletedOn < cutoff) ||
                (j.Status == WsiConversionStatus.Failed && j.CreatedOn < cutoff))
            .ToListAsync(ct);

        if (staleJobs.Count == 0) return 0;

        int deletedCount = 0;

        foreach (var job in staleJobs)
        {
            if (!string.IsNullOrEmpty(job.OriginalStorageId))
            {
                try
                {
                    if (Directory.Exists(job.OriginalStorageId))
                    {
                        Directory.Delete(job.OriginalStorageId, true);
                        logger.LogInformation("Staging hygiene: deleted staging folder {Path} for job {JobId}", job.OriginalStorageId, job.Id);
                        deletedCount++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Staging hygiene: failed to delete staging folder {Path} for job {JobId}", job.OriginalStorageId, job.Id);
                }
            }

            // Remove the job record to keep database clean
            db.Set<WsiConversionJob>().Remove(job);
        }

        await db.SaveChangesAsync(ct);
        return deletedCount;
    }
}
