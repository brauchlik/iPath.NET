using DispatchR.Abstractions.Send;

namespace iPath.Application.Features.TaskAssignments;

public record GetTaskAssignmentByIdQuery(Guid Id)
    : IRequest<GetTaskAssignmentByIdQuery, Task<TaskAssignmentDto>>;
