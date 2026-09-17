using iPath.Application.Features.Questionnaires;
using iPath.Application.Features.Questionnaires.Queries;
using iPath.Application.Features.ServiceRequests;

namespace iPath.EF.Core.FeatureHandlers.Questionnaires.Queries;

public class GetAnswersByCaseHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<GetAnswersByCaseQuery, Task<CaseAnswersDto>>
{
    public async Task<CaseAnswersDto> Handle(GetAnswersByCaseQuery request, CancellationToken ct)
    {
        var visible = new ServiceRequestIsVisibleSpecifications(sess.IsAuthenticated ? sess.User.Id : null);

        var node = await db.ServiceRequests
            .AsNoTracking()
            .Where(x => x.Id == request.ServiceRequestId)
            .Where(visible.ToExpression())
            .Select(x => new { x.Id, x.CreatedOn, x.Description })
            .FirstOrDefaultAsync(ct);

        var empty = new AnswerExtractionStateDto(request.ServiceRequestId, null, false, null, null, null, null, null, null);
        if (node is null) return new CaseAnswersDto(empty, []);

        var qr = node.Description?.Questionnaire;
        var state = new AnswerExtractionStateDto(
            node.Id,
            node.Description?.Title,
            qr is not null && !string.IsNullOrEmpty(qr.Resource),
            qr?.QuestionnaireId,
            qr?.Version,
            qr?.ExtractedOn,
            qr?.ExtractionVersion,
            qr?.ExtractedAnswerCount,
            qr?.ExtractionError,
            qr?.ExtractionDisabled ?? false);

        var answers = await db.ServiceRequestAnswers
            .AsNoTracking()
            .Where(a => a.ServiceRequestId == request.ServiceRequestId)
            .OrderBy(a => a.LinkId)
            .Select(a => new ServiceRequestAnswerDto(a.Id, a.ServiceRequestId, node.Description!.Title, node.CreatedOn,
                a.QuestionnaireId, a.QuestionnaireVersion, a.LinkId, a.CodeSystem, a.Code, a.CodeDisplay, a.OtherCodings,
                a.ValueType, a.Value, a.ValueDisplay, a.Unit, a.ExtractionVersion))
            .ToListAsync(ct);

        return new CaseAnswersDto(state, answers);
    }
}

public class GetAnswersByFilterHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<GetAnswersByFilterQuery, Task<PagedResultList<ServiceRequestAnswerDto>>>
{
    public async Task<PagedResultList<ServiceRequestAnswerDto>> Handle(GetAnswersByFilterQuery request, CancellationToken ct)
    {
        var visible = new ServiceRequestIsVisibleSpecifications(sess.IsAuthenticated ? sess.User.Id : null);
        var groupId = request.GroupId;

        // Keep the join unprojected so ordering can happen on the entities. Ordering the projection
        // instead does not translate: the row type is a positional record, and the dynamic Linq
        // OrderBy("LinkId") rebuilds that record inside the ORDER BY, which EF cannot translate -
        // which failed the whole page, including its default sort.
        var joined = from a in db.ServiceRequestAnswers.AsNoTracking()
                     join sr in db.ServiceRequests.AsNoTracking().Where(visible.ToExpression())
                        on a.ServiceRequestId equals sr.Id
                     where groupId == null || sr.GroupId == groupId
                     select new { Answer = a, Case = sr };

        if (!string.IsNullOrEmpty(request.QuestionnaireId))
        {
            var questionnaireId = request.QuestionnaireId;
            joined = joined.Where(x => x.Answer.QuestionnaireId == questionnaireId);
        }

        var search = request.SearchString?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            joined = joined.Where(x => x.Answer.LinkId.Contains(search)
                                    || (x.Answer.Code != null && x.Answer.Code.Contains(search))
                                    || (x.Answer.CodeDisplay != null && x.Answer.CodeDisplay.Contains(search))
                                    || (x.Answer.Value != null && x.Answer.Value.Contains(search))
                                    || (x.Answer.ValueDisplay != null && x.Answer.ValueDisplay.Contains(search)));
        }

        // the sort labels are the ones the grid sends (see GridDataExtesions.ToSorting)
        var sort = request.Sorting?.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
        joined = sort switch
        {
            "CaseTitle ASC" => joined.OrderBy(x => x.Case.Description!.Title),
            "CaseTitle DESC" => joined.OrderByDescending(x => x.Case.Description!.Title),
            "CaseCreatedOn ASC" => joined.OrderBy(x => x.Case.CreatedOn),
            "CaseCreatedOn DESC" => joined.OrderByDescending(x => x.Case.CreatedOn),
            "Code ASC" => joined.OrderBy(x => x.Answer.Code),
            "Code DESC" => joined.OrderByDescending(x => x.Answer.Code),
            "LinkId DESC" => joined.OrderByDescending(x => x.Answer.LinkId),
            _ => joined.OrderBy(x => x.Answer.LinkId)
        };

        var q = joined.Select(x => new ServiceRequestAnswerDto(x.Answer.Id, x.Answer.ServiceRequestId,
            x.Case.Description!.Title, x.Case.CreatedOn,
            x.Answer.QuestionnaireId, x.Answer.QuestionnaireVersion, x.Answer.LinkId, x.Answer.CodeSystem,
            x.Answer.Code, x.Answer.CodeDisplay, x.Answer.OtherCodings,
            x.Answer.ValueType, x.Answer.Value, x.Answer.ValueDisplay, x.Answer.Unit, x.Answer.ExtractionVersion));

        return await q.ToPagedResultAsync(request, ct);
    }
}

/// <summary>Cases whose response has not been extracted with the current rules, or failed.</summary>
public class GetAnswerExtractionIssuesHandler(iPathDbContext db, IUserSession sess,
    IQuestionnaireAnswerExtractor extractor)
    : IRequestHandler<GetAnswerExtractionIssuesQuery, Task<List<AnswerExtractionStateDto>>>
{
    public async Task<List<AnswerExtractionStateDto>> Handle(GetAnswerExtractionIssuesQuery request, CancellationToken ct)
    {
        var visible = new ServiceRequestIsVisibleSpecifications(sess.IsAuthenticated ? sess.User.Id : null);
        var groupId = request.GroupId;
        var max = request.Max <= 0 ? 200 : request.Max;

        var nodes = await db.ServiceRequests
            .AsNoTracking()
            .Where(visible.ToExpression())
            .Where(x => groupId == null || x.GroupId == groupId)
            .OrderByDescending(x => x.CreatedOn)
            .Select(x => new { x.Id, x.CreatedOn, x.Description })
            .Take(max * 5)
            .ToListAsync(ct);

        var issues = new List<AnswerExtractionStateDto>();
        foreach (var node in nodes)
        {
            var qr = node.Description?.Questionnaire;
            if (qr is null || string.IsNullOrEmpty(qr.Resource)) continue;

            // switched off per questionnaire is intentional, not an issue
            if (qr.ExtractionDisabled) continue;

            var upToDate = qr.ExtractionVersion == extractor.Version && string.IsNullOrEmpty(qr.ExtractionError);
            if (upToDate) continue;

            issues.Add(new AnswerExtractionStateDto(node.Id, node.Description?.Title, true, qr.QuestionnaireId,
                qr.Version, qr.ExtractedOn, qr.ExtractionVersion, qr.ExtractedAnswerCount, qr.ExtractionError, qr.ExtractionDisabled));

            if (issues.Count >= max) break;
        }

        return issues;
    }
}

/// <summary>
/// Pivots the extracted answers: one row per case, one column per concept found in the filtered
/// cohort. Columns come from the data (the concepts that are actually there), not from the
/// questionnaire definitions - a declared catalog with empty columns, ordering and curation is a
/// separate step.
/// </summary>
public class GetAnswerTableHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<GetAnswerTableQuery, Task<AnswerTableDto>>
{
    public async Task<AnswerTableDto> Handle(GetAnswerTableQuery request, CancellationToken ct)
    {
        var visible = new ServiceRequestIsVisibleSpecifications(sess.IsAuthenticated ? sess.User.Id : null);
        var groupId = request.GroupId;

        var filtered = db.ServiceRequestAnswers.AsNoTracking();
        if (!string.IsNullOrEmpty(request.QuestionnaireId))
        {
            var questionnaireId = request.QuestionnaireId;
            filtered = filtered.Where(a => a.QuestionnaireId == questionnaireId);
        }

        var search = request.SearchString?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(a => a.LinkId.Contains(search)
                                        || (a.Code != null && a.Code.Contains(search))
                                        || (a.CodeDisplay != null && a.CodeDisplay.Contains(search))
                                        || (a.Value != null && a.Value.Contains(search))
                                        || (a.ValueDisplay != null && a.ValueDisplay.Contains(search)));
        }

        // which cases take part: those with at least one answer matching the filter
        var matchingCases = db.ServiceRequests.AsNoTracking()
            .Where(visible.ToExpression())
            .Where(sr => groupId == null || sr.GroupId == groupId)
            .Where(sr => filtered.Any(a => a.ServiceRequestId == sr.Id));

        var caseIds = await matchingCases.Select(sr => sr.Id).ToListAsync(ct);

        // Columns and cells come from everything those cases answered, so a case chosen by the search
        // is shown complete instead of as a single matching cell. The form filter still limits both.
        var inCohort = db.ServiceRequestAnswers.AsNoTracking().Where(a => caseIds.Contains(a.ServiceRequestId));
        if (!string.IsNullOrEmpty(request.QuestionnaireId))
        {
            var questionnaireId = request.QuestionnaireId;
            inCohort = inCohort.Where(a => a.QuestionnaireId == questionnaireId);
        }

        // the concepts the cohort actually answered: the columns by default, and the measure of what a
        // selection leaves out
        var observed = await inCohort
            .Select(a => new { a.CodeSystem, a.Code, a.CodeDisplay })
            .Distinct()
            .ToListAsync(ct);

        var selected = request.Columns is { Length: > 0 };

        // An explicit selection (made from the catalog) is rendered as sent - including columns that no
        // case answered, which is what makes two exports comparable. Otherwise the columns are the
        // concepts found in the data.
        var columns = selected
            ? request.Columns!
                .GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Select(c => new AnswerColumnDto(c.Key, c.CodeSystem, c.Code, c.Display ?? c.Code,
                    !AnswerConcept.IsGeneratedKey(c.CodeSystem)))
                .ToList()
            : observed
                .GroupBy(c => AnswerConcept.KeyFor(c.CodeSystem, c.Code), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(c => AnswerConcept.SystemRank(c.CodeSystem))
                .ThenBy(c => c.CodeDisplay ?? c.Code)
                .Select(c => new AnswerColumnDto(
                    AnswerConcept.KeyFor(c.CodeSystem, c.Code),
                    c.CodeSystem,
                    c.Code,
                    c.CodeDisplay ?? c.Code,
                    !AnswerConcept.IsGeneratedKey(c.CodeSystem)))
                .ToList();

        var shownKeys = columns.Select(c => c.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var outsideColumns = selected
            ? observed.Select(c => AnswerConcept.KeyFor(c.CodeSystem, c.Code))
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .Count(k => !shownKeys.Contains(k))
            : 0;

        var pageSize = request.PageSize is > 0 ? request.PageSize.Value : 25;

        var page = await matchingCases
            .Where(sr => caseIds.Contains(sr.Id))
            .OrderBy(sr => sr.CreatedOn)
            .Skip(request.Page * pageSize)
            .Take(pageSize)
            .Select(sr => new
            {
                sr.Id,
                Title = sr.Description!.Title,
                sr.CreatedOn,
                AccessionNo = sr.Description!.AccessionNo,
                BodySite = sr.Description!.BodySite!.Display
            })
            .ToListAsync(ct);

        var pageCaseIds = page.Select(p => p.Id).ToList();
        var answers = await inCohort
            .Where(a => pageCaseIds.Contains(a.ServiceRequestId))
            .Select(a => new { a.ServiceRequestId, a.CodeSystem, a.Code, a.ValueType, a.Value, a.ValueDisplay, a.Unit })
            .ToListAsync(ct);

        // one cell per (case, concept); repeating items are joined
        var cellsByCase = answers
            .GroupBy(a => a.ServiceRequestId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(a => AnswerConcept.KeyFor(a.CodeSystem, a.Code), StringComparer.OrdinalIgnoreCase)
                      .ToDictionary(
                          gg => gg.Key,
                          gg => AnswerCellFormatter.JoinValues(
                              gg.Select(a => AnswerCellFormatter.Format(a.ValueType, a.Value, a.ValueDisplay, a.Unit))),
                          StringComparer.OrdinalIgnoreCase));

        var rows = page
            .Select(p => new AnswerCaseRowDto(p.Id, p.Title, p.CreatedOn, p.AccessionNo, p.BodySite,
                cellsByCase.TryGetValue(p.Id, out var cells) ? cells : []))
            .ToList();

        return new AnswerTableDto(columns, new PagedResultList<AnswerCaseRowDto>(caseIds.Count, rows), outsideColumns);
    }
}
