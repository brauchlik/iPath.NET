using iPath.Application.Features.Questionnaires;
using iPath.Application.Features.Questionnaires.Queries;
using iPath.Application.Services;

namespace iPath.EF.Core.FeatureHandlers.Questionnaires.Queries;

public class GetQuestionnaireConformityHandler(QuestionnaireCacheServer cache, IQuestionnaireConformityChecker checker)
    : IRequestHandler<GetQuestionnaireConformityQuery, Task<List<ConformityFinding>>>
{
    public async Task<List<ConformityFinding>> Handle(GetQuestionnaireConformityQuery request, CancellationToken ct)
    {
        var questionnaire = await cache.GetQuestionnaireAsync(request.QuestionnaireId, request.Version);
        return [.. checker.Check(questionnaire)];
    }
}
