namespace iPath.EF.Core.FeatureHandlers.Nodes.Commands;


public class CreateNodeAnnotationCommandHandler(iPathDbContext db, IMediator mediator, IUserSession sess)
    : IRequestHandler<CreateNodeAnnotationCommand, Task<AnnotationDto>>
{
    public async Task<AnnotationDto> Handle(CreateNodeAnnotationCommand request, CancellationToken ct)
    {
        Guard.Against.NullOrEmpty(request.Text + request.QuestionnaireResponse);

        var node = await db.Nodes.FindAsync(request.NodeId);
        Guard.Against.NotFound(request.NodeId, node);

        if (!sess.IsAdmin)
        {
            // TODO: check authorization. Who may add Annotations ???

        }

        var a = node.CreateNodeAnnotation(request, sess.User.Id);
        db.Nodes.Update(node);
        await db.SaveChangesAsync(ct);

        // update user NodeVisit
        await mediator.Send(new UpdateNodeVisitCommand(node.Id), ct);

        return a.ToDto();
    }
}