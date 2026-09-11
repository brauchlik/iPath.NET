namespace iPath.Application.Features;

public record DeleteQuestionnaireCommand(Guid Id)
    : IRequest<DeleteQuestionnaireCommand, Task<Guid>>;
