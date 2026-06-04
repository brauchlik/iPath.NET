using DispatchR;
using iPath.Application.Features.TaskAssignments;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Queries;

public class GetCaseTaskAssignmentsHandler(
    iPathDbContext db,
    ILogger<GetCaseTaskAssignmentsHandler> logger)
    : IRequestHandler<GetCaseTaskAssignmentsQuery, Task<IReadOnlyList<TaskAssignmentDto>>>
{
    public async Task<IReadOnlyList<TaskAssignmentDto>> Handle(GetCaseTaskAssignmentsQuery request, CancellationToken ct)
    {
        var results = await db.TaskAssignments
            .AsNoTracking()
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .Where(t => t.ServiceRequestId == request.ServiceRequestId)
            .OrderByDescending(t => t.CreatedOn)
            .ToListAsync(ct);

        return results.Select(t => t.ToDto()).ToList();
    }
}
