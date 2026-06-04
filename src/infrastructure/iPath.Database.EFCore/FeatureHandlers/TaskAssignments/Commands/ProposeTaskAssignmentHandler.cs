using DispatchR;
using iPath.Application.Contracts;
using iPath.Application.Exceptions;
using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Commands;

public class ProposeTaskAssignmentHandler(
    iPathDbContext db,
    IUserSession sess,
    IMediator mediator,
    ILogger<ProposeTaskAssignmentHandler> logger)
    : IRequestHandler<ProposeTaskAssignmentCommand, Task<TaskAssignmentDto>>
{
    public async Task<TaskAssignmentDto> Handle(ProposeTaskAssignmentCommand request, CancellationToken ct)
    {
        var sr = await db.ServiceRequests.FindAsync([request.ServiceRequestId], ct);
        Guard.Against.NotFound(request.ServiceRequestId, sr);

        if (!sess.IsAdmin)
        {
            sess.AssertInGroup(sr.GroupId);

            if (request.Mode == eTaskAssignmentMode.ModeratorSuggested)
            {
                var gm = await db.Set<GroupMember>()
                    .FirstOrDefaultAsync(m => m.GroupId == sr.GroupId && m.UserId == sess.User.Id, ct);
                if (gm?.Role != eMemberRole.Moderator)
                    throw new NotAllowedException("Only moderators can propose moderator-suggested assignments");
            }
        }

        var user = await db.Users.FindAsync([request.AssignedToUserId], ct);
        Guard.Against.NotFound(request.AssignedToUserId, user);

        var ta = new TaskAssignment
        {
            Id = Guid.CreateVersion7(),
            ServiceRequestId = request.ServiceRequestId,
            AssignedToUserId = request.AssignedToUserId,
            AssignedByUserId = sess.User.Id,
            Type = eTaskType.DiagnosticReview,
            Mode = request.Mode,
            Status = request.Mode == eTaskAssignmentMode.DirectAssigned ? eTaskStatus.Assigned : eTaskStatus.Proposed,
            Notes = request.Notes,
            CreatedOn = DateTime.UtcNow
        };

        if (ta.Status == eTaskStatus.Assigned)
            ta.AcceptedOn = DateTime.UtcNow;

        db.TaskAssignments.Add(ta);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("TaskAssignment {Id} proposed for SR {SrId} to user {UserId}", ta.Id, request.ServiceRequestId, request.AssignedToUserId);
        return await mediator.Send(new GetTaskAssignmentByIdQuery(ta.Id), ct);
    }
}
