public class CreateNodeCommandHandler(iPathDbContext db, IUserSession sess, IMediator mediator)
    : IRequestHandler<CreateNodeCommand, Task<NodeListDto>>
{
    public async Task<NodeListDto> Handle(CreateNodeCommand request, CancellationToken ct)
    {
        if (!sess.IsAdmin)
            sess.AssertInGroup(request.GroupId);

        var group = await db.Groups.FindAsync(request.GroupId, ct);
        Guard.Against.NotFound(request.GroupId, group);

        await using var tran = await db.Database.BeginTransactionAsync(ct);

        var node = new Node
        {
            Id = request.NodeId.HasValue ? request.NodeId.Value : Guid.CreateVersion7(),
            CreatedOn = DateTime.UtcNow,
            LastModifiedOn = DateTime.UtcNow,
            GroupId = request.GroupId,
            OwnerId = sess.User.Id,
            Description = request.Description,
            NodeType = request.NodeType
        };

        await db.Nodes.AddAsync(node, ct);
        var evt = await db.CreateEventAsync<RootNodeCreatedEvent, CreateNodeCommand>(request, node.Id, sess.User.Id);
        evt.GroupId = request.GroupId;
        await db.SaveChangesAsync(ct);

        await tran.CommitAsync(ct);

        // publish domain events
        await mediator.Publish(evt, ct);

        return node.ToListDto();
    }
}