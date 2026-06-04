using DispatchR.Abstractions.Send;

namespace iPath.Application.Features.TaskAssignments;

public record CreateFollowUpTaskCommand(Guid ServiceRequestId, string? Notes = null)
    : IRequest<CreateFollowUpTaskCommand, Task<TaskAssignmentDto>>;
