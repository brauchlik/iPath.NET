using DispatchR.Abstractions.Send;

namespace iPath.Application.Features.TaskAssignments;

public record GetCaseTaskAssignmentsQuery(Guid ServiceRequestId)
    : IRequest<GetCaseTaskAssignmentsQuery, Task<IReadOnlyList<TaskAssignmentDto>>>;
