using DispatchR;
using iPath.Application.Contracts;
using iPath.Application.Exceptions;
using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Commands;

public class ReturnTaskAssignmentHandler(
    iPathDbContext db,
    IUserSession sess,
    IMediator mediator,
    ILogger<ReturnTaskAssignmentHandler> logger)
    : IRequestHandler<ReturnTaskAssignmentCommand, Task<TaskAssignmentDto>>
{
    public async Task<TaskAssignmentDto> Handle(ReturnTaskAssignmentCommand request, CancellationToken ct)
    {
        var ta = await db.TaskAssignments.FindAsync([request.TaskAssignmentId], ct);
        Guard.Against.NotFound(request.TaskAssignmentId, ta);

        if (ta.AssignedToUserId != sess.User.Id && !sess.IsAdmin)
            throw new NotAllowedException("Only the assigned user can return this task");

        if (ta.Status is not (eTaskStatus.Assigned or eTaskStatus.InProgress))
            throw new InvalidOperationException($"Cannot return task in status {ta.Status}");

        ta.ReturnForReassignment();
        await db.SaveChangesAsync(ct);

        logger.LogInformation("TaskAssignment {Id} returned for reassignment by user {UserId}", ta.Id, sess.User.Id);
        return await mediator.Send(new GetTaskAssignmentByIdQuery(ta.Id), ct);
    }
}
