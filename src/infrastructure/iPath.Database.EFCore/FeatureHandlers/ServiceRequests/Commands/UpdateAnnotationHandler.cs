using iPath.Application.Features.Annotations;

namespace iPath.EF.Core.FeatureHandlers.ServiceRequests.Commands;

public class UpdateAnnotationHandler(iPathDbContext db, IMediator mediator, IUserSession sess)
    : IRequestHandler<UpdateAnnotationCommand, Task<AnnotationDto>>
{
    public async Task<AnnotationDto> Handle(UpdateAnnotationCommand request, CancellationToken ct)
    {
        if (!request.Data.ValidateInput())
        {
            throw new ArgumentException("Invalid Annotation Data");
        }

        var annotation = await db.Annotations.FindAsync(request.annotationId, ct);
        Guard.Against.NotFound(request.annotationId, annotation);

        // Check Document
        if (request.Data.DocumentId.HasValue)
        {
            var document = await db.Documents.FindAsync(request.Data.DocumentId.Value, ct);
            Guard.Against.NotFound(request.Data.DocumentId.Value, document);

            if (document.ServiceRequestId != annotation.Id)
                throw new ArgumentException("Child doe nbot belong to RootNode");
        }

        if (!sess.IsAdmin)
        {
            // TODO: check authorization. Who may add Annotations ???
            if (annotation.OwnerId != sess.User.Id)
            {
                throw new NotAllowedException();
            }
        }

        // update data
        annotation.Data = request.Data;
        annotation.LastModifiedOn = DateTime.UtcNow;
        annotation.CreateEvent<AnnotationUpdatedEvent, UpdateAnnotationCommand>(request, sess.User.Id);

        db.Annotations.Update(annotation);
        await db.SaveChangesAsync(ct);

        return annotation.ToDto();
    }
}
