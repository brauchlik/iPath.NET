using DispatchR;
using iPath.Application.Contracts;
using iPath.Application.Exceptions;
using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Commands;

public class CreateFollowUpTaskHandler(
    iPathDbContext db,
    IUserSession sess,
    IMediator mediator,
    ILogger<CreateFollowUpTaskHandler> logger)
    : IRequestHandler<CreateFollowUpTaskCommand, Task<TaskAssignmentDto>>
{
    public async Task<TaskAssignmentDto> Handle(CreateFollowUpTaskCommand request, CancellationToken ct)
    {
        var sr = await db.ServiceRequests
            .Include(x => x.Owner)
            .FirstOrDefaultAsync(x => x.Id == request.ServiceRequestId, ct);
        Guard.Against.NotFound(request.ServiceRequestId, sr);

        if (!sess.IsAdmin)
            sess.AssertInGroup(sr.GroupId);

        var ta = new TaskAssignment
        {
            Id = Guid.CreateVersion7(),
            ServiceRequestId = request.ServiceRequestId,
            AssignedToUserId = sr.OwnerId,
            AssignedByUserId = sess.User.Id,
            Type = eTaskType.FollowUp,
            Mode = eTaskAssignmentMode.DirectAssigned,
            Status = eTaskStatus.Assigned,
            Notes = request.Notes,
            AcceptedOn = DateTime.UtcNow,
            CreatedOn = DateTime.UtcNow
        };

        db.TaskAssignments.Add(ta);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("FollowUp Task {Id} created for SR {SrId} to owner {OwnerId}", ta.Id, request.ServiceRequestId, sr.OwnerId);
        return await mediator.Send(new GetTaskAssignmentByIdQuery(ta.Id), ct);
    }
}
