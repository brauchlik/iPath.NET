using iPath.Application.Exceptions;

namespace iPath.EF.Core.FeatureHandlers.Nodes.Commands;

public class DeleteAnnotationCommandHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<DeleteAnnotationCommand, Task<Guid>>
{
    public async Task<Guid> Handle(DeleteAnnotationCommand request, CancellationToken ct)
    {
        if (!sess.IsAdmin) throw new NotAllowedException();

        await using var tran = await db.Database.BeginTransactionAsync(ct);

        var annotation = await db.Annotations.FindAsync(request.AnnotationId);
        Guard.Against.NotFound(request.AnnotationId, annotation);
        db.Annotations.Remove(annotation);
        if (annotation.ServiceRequestId.HasValue) {
            var evt = await db.CreateEventAsync <AnnotationDeletedEvent, DeleteAnnotationCommand> (request, annotation.ServiceRequestId.Value, sess);
        }
        await db.SaveChangesAsync(ct);
        await tran.CommitAsync(ct);

        return annotation.Id;
    }
}