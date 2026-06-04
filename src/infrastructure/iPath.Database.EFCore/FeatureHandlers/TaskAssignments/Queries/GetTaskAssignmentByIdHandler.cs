using DispatchR;
using iPath.Application.Features.TaskAssignments;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Queries;

public class GetTaskAssignmentByIdHandler(
    iPathDbContext db,
    ILogger<GetTaskAssignmentByIdHandler> logger)
    : IRequestHandler<GetTaskAssignmentByIdQuery, Task<TaskAssignmentDto>>
{
    public async Task<TaskAssignmentDto> Handle(GetTaskAssignmentByIdQuery request, CancellationToken ct)
    {
        var ta = await db.TaskAssignments
            .AsNoTracking()
            .Include(t => t.ServiceRequest).ThenInclude(s => s.Group)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .FirstOrDefaultAsync(t => t.Id == request.Id, ct);

        Guard.Against.NotFound(request.Id, ta);
        return ta.ToDto();
    }
}
