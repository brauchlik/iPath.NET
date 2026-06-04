using iPath.Application.Features.TaskAssignments;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Queries;

public class GetUserTaskAssignmentsHandler(
    iPathDbContext db,
    IUserSession sess,
    ILogger<GetUserTaskAssignmentsHandler> logger)
    : IRequestHandler<GetUserTaskAssignmentsQuery, Task<PagedResultList<TaskAssignmentDto>>>
{
    public async Task<PagedResultList<TaskAssignmentDto>> Handle(GetUserTaskAssignmentsQuery request, CancellationToken ct)
    {
        var userId = request.UserId ?? sess.User.Id;

        var query = db.TaskAssignments
            .AsNoTracking()
            .Include(t => t.ServiceRequest).ThenInclude(s => s.Group)
            .Include(t => t.ServiceRequest).ThenInclude(s => s.Owner)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .Where(t => t.AssignedToUserId == userId);

        if (request.StatusFilter.HasValue)
            query = query.Where(t => t.Status == request.StatusFilter.Value);

        query = query.ApplyQuery(request, "CreatedOn DESC");

        var dto = query.Select(t => new TaskAssignmentDto
        {
            Id = t.Id,
            ServiceRequestId = t.ServiceRequestId,
            Title = t.ServiceRequest!.Description!.Title,
            GroupId = t.ServiceRequest.GroupId,
            GroupName = t.ServiceRequest.Group.Name,
            AssignedToUserId = t.AssignedToUserId,
            AssignedToUsername = t.AssignedToUser!.UserName,
            AssignedByUserId = t.AssignedByUserId,
            AssignedByUsername = t.AssignedByUser!.UserName,
            Type = t.Type.ToString(),
            Mode = t.Mode.ToString(),
            Status = t.Status.ToString(),
            Notes = t.Notes,
            CreatedOn = t.CreatedOn,
            AcceptedOn = t.AcceptedOn,
            CompletedOn = t.CompletedOn,
            Deadline = t.Deadline,
            ServiceRequest = request.IncludeServiceRequest ? t.ServiceRequest.ToListDto() : null
        });

        return await dto.ToPagedResultAsync(request, ct);
    }
}
