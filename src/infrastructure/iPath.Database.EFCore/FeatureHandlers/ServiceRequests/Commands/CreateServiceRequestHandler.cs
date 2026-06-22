using iPath.Application.Features.ServiceRequests;

public class CreateServiceRequestCommandHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<CreateServiceRequestCommand, Task<ServiceRequestDto>>
{
    public async Task<ServiceRequestDto> Handle(CreateServiceRequestCommand request, CancellationToken ct)
    {
        var ownerId = request.OwnerId ?? sess.User.Id;
        if (!sess.IsAdmin)
            sess.AssertInGroup(request.GroupId);

        var group = await db.Groups.FindAsync(request.GroupId, ct);
        Guard.Against.NotFound(request.GroupId, group);

        var node = ServiceRequestCommandExtensions.CreateRequest(request, ownerId);
        await db.ServiceRequests.AddAsync(node, ct);
        await db.SaveChangesAsync(ct);

        var owner = await db.Users.FindAsync([ownerId], ct);
        node.Owner = owner!;
        return node.ToDto();
    }
}