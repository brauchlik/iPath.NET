using iPath.Application.Features.ServiceRequests;

public class CreateServiceRequestCommandHandler(iPathDbContext db, IUserSession sess, IMediator mediator)
    : IRequestHandler<CreateServiceRequestCommand, Task<ServiceRequestDto>>
{
    public async Task<ServiceRequestDto> Handle(CreateServiceRequestCommand request, CancellationToken ct)
    {
        if (!sess.IsAdmin)
            sess.AssertInGroup(request.GroupId);

        var group = await db.Groups.FindAsync(request.GroupId, ct);
        Guard.Against.NotFound(request.GroupId, group);

        var node = iPath.Application.Features.ServiceRequests.ServiceRequestCommandExtensions.CreateRequest(request, sess.User.Id);
        await db.ServiceRequests.AddAsync(node, ct);
        await db.SaveChangesAsync(ct);

        return await mediator.Send(new GetServiceRequestByIdQuery(node.Id), ct);
    }
}