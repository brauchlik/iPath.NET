using iPath.Application.Features.Questionnaires;

namespace iPath.Application.Features.Questionnaires.Queries;

public record GetQuestionnaireConformityQuery(string QuestionnaireId, int? Version = null)
    : IRequest<GetQuestionnaireConformityQuery, Task<List<ConformityFinding>>>;
