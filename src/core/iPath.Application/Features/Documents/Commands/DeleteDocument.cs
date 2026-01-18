namespace iPath.Application.Features.Documents;

public record DeleteDocumentCommand(Guid DocumentId)
    : IRequest<DeleteDocumentCommand, Task>
    , IEventInput
{
    public string ObjectName => nameof(DocumentNode);
}

