namespace iPath.Application.Features.Documents;

public record UpdateDocumenttCommand(Guid DocumentId, NodeFile? Description, bool? IsDraft)
    : IRequest<UpdateDocumenttCommand, Task>
    , IEventInput
{
    public string ObjectName => nameof(DocumentNode);
}
