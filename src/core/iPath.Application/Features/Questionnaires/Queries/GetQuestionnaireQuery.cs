namespace iPath.Application.Features;

public record GetQuestionnaireQuery(string QuestionnaireId, int? Version = null)
    : IRequest<GetQuestionnaireQuery, Task<QuestionnaireEntity>>;
