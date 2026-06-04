using DispatchR;
using iPath.Application.Contracts;
using iPath.Application.Exceptions;
using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Commands;

public class AcceptTaskAssignmentHandler(
    iPathDbContext db,
    IUserSession sess,
    IMediator mediator,
    ILogger<AcceptTaskAssignmentHandler> logger)
    : IRequestHandler<AcceptTaskAssignmentCommand, Task<TaskAssignmentDto>>
{
    public async Task<TaskAssignmentDto> Handle(AcceptTaskAssignmentCommand request, CancellationToken ct)
    {
        var ta = await db.TaskAssignments.FindAsync([request.TaskAssignmentId], ct);
        Guard.Against.NotFound(request.TaskAssignmentId, ta);

        if (ta.AssignedToUserId != sess.User.Id && !sess.IsAdmin)
            throw new NotAllowedException("Only the assigned user can accept this task");

        if (ta.Status != eTaskStatus.Proposed)
            throw new InvalidOperationException($"Cannot accept task in status {ta.Status}");

        ta.Accept();
        await db.SaveChangesAsync(ct);

        logger.LogInformation("TaskAssignment {Id} accepted by user {UserId}", ta.Id, sess.User.Id);
        return await mediator.Send(new GetTaskAssignmentByIdQuery(ta.Id), ct);
    }
}
