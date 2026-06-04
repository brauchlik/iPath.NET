using DispatchR.Abstractions.Send;
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public record ProposeTaskAssignmentCommand(Guid ServiceRequestId, Guid AssignedToUserId, eTaskAssignmentMode Mode, string? Notes = null)
    : IRequest<ProposeTaskAssignmentCommand, Task<TaskAssignmentDto>>;
