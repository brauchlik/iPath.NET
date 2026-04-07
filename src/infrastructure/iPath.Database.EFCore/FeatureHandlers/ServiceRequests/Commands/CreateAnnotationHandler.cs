using iPath.Application.Features.Annotations;

namespace iPath.EF.Core.FeatureHandlers.ServiceRequests.Commands;


public class CreateAnnotationCommandHandler(iPathDbContext db, IMediator mediator, IUserSession sess)
    : IRequestHandler<CreateAnnotationCommand, Task<AnnotationDto>>
{
    public async Task<AnnotationDto> Handle(CreateAnnotationCommand request, CancellationToken ct)
    {
        if (!request.Data.ValidateInput())
        {
            throw new ArgumentException("Invalid Annotation Data");
        }

        var serviceRequest = await db.ServiceRequests.FindAsync(request.requestId);
        Guard.Against.NotFound(request.requestId, serviceRequest);

        if (request.Data.DocumentId.HasValue)
        {
            var document = await db.Documents.FindAsync(request.Data.DocumentId.Value);
            Guard.Against.NotFound(request.Data.DocumentId.Value, document);

            if (document.ServiceRequestId != serviceRequest.Id)
                throw new ArgumentException("Child doe nbot belong to RootNode");
        }

        if (!sess.IsAdmin)
        {
            // TODO: check authorization. Who may add Annotations ???

        }

        var a = serviceRequest.CreateNodeAnnotation(request, sess.User.Id);
        a.CreateEvent<AnnotationCreatedEvent, CreateAnnotationCommand>(request, sess.User.Id);        db.ServiceRequests.Update(serviceRequest);
        await db.SaveChangesAsync(ct);

        // update user NodeVisit
        await mediator.Send(new UpdateServiceRequestVisitCommand(serviceRequest.Id), ct);

        return a.ToDto();
    }
}