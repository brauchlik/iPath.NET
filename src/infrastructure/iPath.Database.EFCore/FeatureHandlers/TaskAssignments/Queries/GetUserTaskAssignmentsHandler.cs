using DispatchR;
using iPath.Application.Contracts;
using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Queries;

public class GetUserTaskAssignmentsHandler(
    iPathDbContext db,
    IUserSession sess,
    ILogger<GetUserTaskAssignmentsHandler> logger)
    : IRequestHandler<GetUserTaskAssignmentsQuery, Task<IReadOnlyList<TaskAssignmentDto>>>
{
    public async Task<IReadOnlyList<TaskAssignmentDto>> Handle(GetUserTaskAssignmentsQuery request, CancellationToken ct)
    {
        var userId = request.UserId ?? sess.User.Id;

        var query = db.TaskAssignments
            .AsNoTracking()
            .Include(t => t.ServiceRequest).ThenInclude(s => s.Group)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .Where(t => t.AssignedToUserId == userId);

        if (request.StatusFilter.HasValue)
            query = query.Where(t => t.Status == request.StatusFilter.Value);

        var results = await query
            .OrderByDescending(t => t.CreatedOn)
            .ToListAsync(ct);

        return results.Select(t => t.ToDto()).ToList();
    }
}
