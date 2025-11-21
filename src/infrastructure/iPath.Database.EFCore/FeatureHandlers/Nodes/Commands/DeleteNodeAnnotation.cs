using iPath.Application.Exceptions;

namespace iPath.EF.Core.FeatureHandlers.Nodes.Commands;

public class DeleteNodeAnnotationCommandHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<DeleteNodeAnnotationCommand, Task<Guid>>
{
    public async Task<Guid> Handle(DeleteNodeAnnotationCommand request, CancellationToken ct)
    {
        if (!sess.IsAdmin) throw new NotAllowedException();

        await using var tran = await db.Database.BeginTransactionAsync(ct);

        var annotation = await db.Annotations.FindAsync(request.AnnotationId);
        Guard.Against.NotFound(request.AnnotationId, annotation);
        db.Annotations.Remove(annotation);
        if (annotation.NodeId.HasValue) {
            var evt = await db.CreateEventAsync <NodeAnnotationDeletedEvent, DeleteNodeAnnotationCommand> (request, annotation.NodeId.Value);
        }
        await db.SaveChangesAsync(ct);
        await tran.CommitAsync(ct);

        return annotation.Id;
    }
}