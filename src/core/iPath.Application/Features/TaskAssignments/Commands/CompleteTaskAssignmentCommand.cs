using DispatchR.Abstractions.Send;

namespace iPath.Application.Features.TaskAssignments;

public record CompleteTaskAssignmentCommand(Guid TaskAssignmentId)
    : IRequest<CompleteTaskAssignmentCommand, Task<TaskAssignmentDto>>;
