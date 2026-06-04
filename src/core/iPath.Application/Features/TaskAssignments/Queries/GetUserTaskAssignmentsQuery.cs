using iPath.Application.Querying;
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public class GetUserTaskAssignmentsQuery : PagedQuery<TaskAssignmentDto>
    , IRequest<GetUserTaskAssignmentsQuery, Task<PagedResultList<TaskAssignmentDto>>>
{
    public Guid? UserId { get; set; }
    public eTaskStatus? StatusFilter { get; set; }
    public bool IncludeServiceRequest { get; set; }
}
