using DispatchR.Abstractions.Send;

namespace iPath.Application.Features.TaskAssignments;

public record ReturnTaskAssignmentCommand(Guid TaskAssignmentId)
    : IRequest<ReturnTaskAssignmentCommand, Task<TaskAssignmentDto>>;
