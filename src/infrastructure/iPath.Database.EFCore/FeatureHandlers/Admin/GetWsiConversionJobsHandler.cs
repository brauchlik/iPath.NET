using iPath.Application.Features.Admin;
using iPath.EF.Core.Database;
using iPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using DispatchR.Abstractions.Send;

namespace iPath.EF.Core.FeatureHandlers.Admin;

public class GetWsiConversionJobsHandler(iPathDbContext db)
    : IRequestHandler<GetWsiConversionJobsQuery, Task<List<WsiConversionJobDto>>>
{
    public async Task<List<WsiConversionJobDto>> Handle(GetWsiConversionJobsQuery request, CancellationToken ct)
    {
        var jobs = await db.Set<WsiConversionJob>()
            .Include(j => j.Document)
            .OrderByDescending(j => j.CreatedOn)
            .Take(10)
            .Select(j => new WsiConversionJobDto
            {
                Id = j.Id,
                DocumentId = j.DocumentId,
                Status = j.Status,
                CreatedOn = j.CreatedOn,
                StartedOn = j.StartedOn,
                CompletedOn = j.CompletedOn,
                ErrorMessage = j.ErrorMessage,
                RetryCount = j.RetryCount,
                Filename = j.Document.File != null ? j.Document.File.Filename : "Unknown"
            })
            .ToListAsync(ct);

        return jobs;
    }
}
