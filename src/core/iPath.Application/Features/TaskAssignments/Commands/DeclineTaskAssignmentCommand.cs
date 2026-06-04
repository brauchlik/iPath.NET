using DispatchR.Abstractions.Send;

namespace iPath.Application.Features.TaskAssignments;

public record DeclineTaskAssignmentCommand(Guid TaskAssignmentId)
    : IRequest<DeclineTaskAssignmentCommand, Task<TaskAssignmentDto>>;
