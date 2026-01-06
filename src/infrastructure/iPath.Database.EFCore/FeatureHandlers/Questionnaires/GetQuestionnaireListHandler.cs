namespace iPath.EF.Core.FeatureHandlers.Questionnaires;

public class GetQuestionnaireListHandler(iPathDbContext db)
     : IRequestHandler<GetQuestionnaireListQuery, Task<PagedResultList<QuestionnaireListDto>>>
{
    public async Task<PagedResultList<QuestionnaireListDto>> Handle(GetQuestionnaireListQuery request, CancellationToken ct)
    {
        var q = db.Questionnaires.AsNoTracking();
        if (!request.AllVersions)
        {
            q = q.Where(x => x.IsActive);
        }
        else
        {
            q.OrderByDescending(x => x.Version);
        }

        q.ApplyQuery(request);

        var projected = q.Select(x => new QuestionnaireListDto(x.Id, x.QuestionnaireId, x.Name, x.Version, x.IsActive));
        return await projected.ToPagedResultAsync(request, ct);    
    }
}