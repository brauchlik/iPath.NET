using iPath.Application.Features.Questionnaires;
using iPath.Application.Features.Questionnaires.Queries;
using iPath.Application.Services;

namespace iPath.EF.Core.FeatureHandlers.Questionnaires.Queries;

/// <summary>
/// Builds the catalog of one group: its case description forms (assigned and/or used by its cases)
/// and the concepts those forms can record, from their active definitions, annotated with what the
/// group's cases actually answered.
/// </summary>
public class GetAnswerCatalogHandler(iPathDbContext db, IUserSession sess,
    QuestionnaireCacheServer cache, IQuestionnaireConformityChecker checker)
    : IRequestHandler<GetAnswerCatalogQuery, Task<AnswerCatalogDto>>
{
    public async Task<AnswerCatalogDto> Handle(GetAnswerCatalogQuery request, CancellationToken ct)
    {
        var groupId = request.GroupId;
        var warnings = new List<string>();
        var visible = new ServiceRequestIsVisibleSpecifications(sess.IsAuthenticated ? sess.User.Id : null);

        // the group's cases and the form each was answered against
        var cases = await db.ServiceRequests.AsNoTracking()
            .Where(visible.ToExpression())
            .Where(sr => sr.GroupId == groupId)
            .Select(sr => new
            {
                sr.Id,
                QuestionnaireId = sr.Description!.Questionnaire!.QuestionnaireId,
                Version = sr.Description!.Questionnaire!.Version
            })
            .ToListAsync(ct);

        var answered = cases.Where(c => !string.IsNullOrEmpty(c.QuestionnaireId)).ToList();

        // forms assigned to this group for case descriptions, in the priority the group set
        var assigned = await db.Set<QuestionnaireForGroup>()
            .AsNoTracking()
            .Where(q => q.GroupId == groupId && q.Usage == eQuestionnaireUsage.CaseDescription)
            .Select(q => new { q.Questionnaire.QuestionnaireId, q.Questionnaire.Name, q.Priority })
            .OrderBy(q => q.Priority).ThenBy(q => q.Name)
            .ToListAsync(ct);

        // the active definition of every form that is assigned or used
        var formIds = assigned.Select(a => a.QuestionnaireId)
            .Concat(answered.Select(c => c.QuestionnaireId!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var actives = await db.Questionnaires.AsNoTracking()
            .Where(q => formIds.Contains(q.QuestionnaireId) && q.IsActive)
            .Select(q => new { q.QuestionnaireId, q.Name, q.Version })
            .ToListAsync(ct);

        var definitions = new List<CatalogFormInput>();
        foreach (var form in actives)
        {
            var definition = await cache.GetQuestionnaireAsync(form.QuestionnaireId, form.Version);
            if (definition is null)
            {
                warnings.Add($"Form {form.QuestionnaireId} version {form.Version} could not be loaded - its questions are missing from the catalog");
                continue;
            }

            definitions.Add(new CatalogFormInput(form.QuestionnaireId, form.Name, form.Version, definition));
        }

        // per concept: how many of the group's cases answered it
        var caseIds = cases.Select(c => c.Id).ToList();
        var answers = await db.ServiceRequestAnswers.AsNoTracking()
            .Where(a => caseIds.Contains(a.ServiceRequestId))
            .Select(a => new { a.ServiceRequestId, a.CodeSystem, a.Code })
            .ToListAsync(ct);

        var counts = answers
            .GroupBy(a => AnswerConcept.KeyFor(a.CodeSystem, a.Code), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => new CatalogAnswerCounts(g.Select(a => a.ServiceRequestId).Distinct().Count(), g.Count()),
                StringComparer.OrdinalIgnoreCase);

        var concepts = AnswerCatalogBuilder.BuildConcepts(definitions, counts);

        var forms = new List<CatalogFormDto>();
        foreach (var form in assigned)
        {
            forms.Add(BuildForm(form.QuestionnaireId, form.Name, assigned: true));
        }

        foreach (var formId in answered.Select(c => c.QuestionnaireId!).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (assigned.Any(a => string.Equals(a.QuestionnaireId, formId, StringComparison.OrdinalIgnoreCase))) continue;

            forms.Add(BuildForm(formId, null, assigned: false));
            warnings.Add($"Form {formId} is used by cases of this group but is not assigned to it");
        }

        // answers that no active form defines: the case was answered against another version
        var inCatalog = concepts.Select(c => c.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orphaned = counts.Keys.Count(k => !inCatalog.Contains(k));
        if (orphaned > 0)
        {
            warnings.Add($"{orphaned} recorded concept(s) are not in the active forms - those cases were answered against another version");
        }

        return new AnswerCatalogDto(forms, concepts, warnings, cases.Count);

        CatalogFormDto BuildForm(string questionnaireId, string? name, bool assigned)
        {
            var active = actives.FirstOrDefault(a => string.Equals(a.QuestionnaireId, questionnaireId, StringComparison.OrdinalIgnoreCase));
            var definition = definitions.FirstOrDefault(d => string.Equals(d.QuestionnaireId, questionnaireId, StringComparison.OrdinalIgnoreCase));
            var casesWithAnswers = answered.Count(c => string.Equals(c.QuestionnaireId, questionnaireId, StringComparison.OrdinalIgnoreCase));

            return new CatalogFormDto(
                questionnaireId,
                name ?? active?.Name ?? questionnaireId,
                active?.Version ?? 0,
                assigned,
                casesWithAnswers,
                definition is null ? [] : checker.Check(definition.Definition));
        }
    }
}
