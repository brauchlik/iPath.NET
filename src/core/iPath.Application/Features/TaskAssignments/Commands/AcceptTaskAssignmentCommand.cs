using DispatchR.Abstractions.Send;

namespace iPath.Application.Features.TaskAssignments;

public record AcceptTaskAssignmentCommand(Guid TaskAssignmentId)
    : IRequest<AcceptTaskAssignmentCommand, Task<TaskAssignmentDto>>;
