using DispatchR.Abstractions.Send;

namespace iPath.Application.Features.TaskAssignments;

public record CancelTaskAssignmentCommand(Guid TaskAssignmentId)
    : IRequest<CancelTaskAssignmentCommand, Task<TaskAssignmentDto>>;
