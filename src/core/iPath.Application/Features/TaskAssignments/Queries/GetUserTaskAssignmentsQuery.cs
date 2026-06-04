using DispatchR.Abstractions.Send;
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public record GetUserTaskAssignmentsQuery(Guid? UserId = null, eTaskStatus? StatusFilter = null)
    : IRequest<GetUserTaskAssignmentsQuery, Task<IReadOnlyList<TaskAssignmentDto>>>;
