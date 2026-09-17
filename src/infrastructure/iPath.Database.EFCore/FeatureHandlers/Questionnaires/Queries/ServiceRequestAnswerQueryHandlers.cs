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

        var q = from a in db.ServiceRequestAnswers.AsNoTracking()
                join sr in db.ServiceRequests.AsNoTracking().Where(visible.ToExpression())
                    on a.ServiceRequestId equals sr.Id
                where groupId == null || sr.GroupId == groupId
                select new ServiceRequestAnswerDto(a.Id, a.ServiceRequestId, sr.Description!.Title, sr.CreatedOn,
                    a.QuestionnaireId, a.QuestionnaireVersion, a.LinkId, a.CodeSystem, a.Code, a.CodeDisplay, a.OtherCodings,
                    a.ValueType, a.Value, a.ValueDisplay, a.Unit, a.ExtractionVersion);

        if (!string.IsNullOrEmpty(request.QuestionnaireId))
        {
            q = q.Where(x => x.QuestionnaireId == request.QuestionnaireId);
        }

        var search = request.SearchString?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            q = q.Where(x => x.LinkId.Contains(search)
                          || (x.Code != null && x.Code.Contains(search))
                          || (x.CodeDisplay != null && x.CodeDisplay.Contains(search))
                          || (x.Value != null && x.Value.Contains(search))
                          || (x.ValueDisplay != null && x.ValueDisplay.Contains(search)));
        }

        var ordered = q.ApplyQuery(request, "LinkId");
        return await ordered.ToPagedResultAsync(request, ct);
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
