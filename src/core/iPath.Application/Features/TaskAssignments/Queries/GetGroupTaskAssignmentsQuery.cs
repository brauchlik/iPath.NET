using DispatchR.Abstractions.Send;
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public record GetGroupTaskAssignmentsQuery(Guid GroupId, eTaskStatus? StatusFilter = null)
    : IRequest<GetGroupTaskAssignmentsQuery, Task<IReadOnlyList<TaskAssignmentDto>>>;
