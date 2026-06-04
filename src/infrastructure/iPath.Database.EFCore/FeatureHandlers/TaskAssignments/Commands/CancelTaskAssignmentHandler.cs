using DispatchR;
using iPath.Application.Contracts;
using iPath.Application.Exceptions;
using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Commands;

public class CancelTaskAssignmentHandler(
    iPathDbContext db,
    IUserSession sess,
    IMediator mediator,
    ILogger<CancelTaskAssignmentHandler> logger)
    : IRequestHandler<CancelTaskAssignmentCommand, Task<TaskAssignmentDto>>
{
    public async Task<TaskAssignmentDto> Handle(CancelTaskAssignmentCommand request, CancellationToken ct)
    {
        var ta = await db.TaskAssignments.FindAsync([request.TaskAssignmentId], ct);
        Guard.Against.NotFound(request.TaskAssignmentId, ta);

        var sr = await db.ServiceRequests.FindAsync([ta.ServiceRequestId], ct);
        if (!sess.IsAdmin)
            sess.AssertInGroup(sr!.GroupId);

        var gm = sess.User.groups?.FirstOrDefault(m => m.GroupId == sr!.GroupId);
        if (gm?.Role != eMemberRole.Moderator && !sess.IsAdmin)
            throw new NotAllowedException("Only moderators and admins can cancel tasks");

        ta.Cancel();
        await db.SaveChangesAsync(ct);

        logger.LogInformation("TaskAssignment {Id} cancelled by user {UserId}", ta.Id, sess.User.Id);
        return await mediator.Send(new GetTaskAssignmentByIdQuery(ta.Id), ct);
    }
}
