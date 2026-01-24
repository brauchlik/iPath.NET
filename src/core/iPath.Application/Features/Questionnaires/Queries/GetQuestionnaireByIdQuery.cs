namespace iPath.Application.Features;

public record GetQuestionnaireByIdQuery(Guid Id)
    : IRequest<GetQuestionnaireByIdQuery, Task<QuestionnaireEntity>>;
