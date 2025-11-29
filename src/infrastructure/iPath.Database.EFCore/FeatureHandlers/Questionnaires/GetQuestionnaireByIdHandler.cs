
namespace iPath.EF.Core.FeatureHandlers.Questionnaires;

public class GetQuestionnaireByIdHandler(iPathDbContext db)
     : IRequestHandler<GetQuestionnaireByIdQuery, Task<Questionnaire>>
{
    public async Task<Questionnaire> Handle(GetQuestionnaireByIdQuery request, CancellationToken cancellationToken)
    {
        return await db.Questionnaires.AsNoTracking().FirstOrDefaultAsync(q => q.Id == request.Id);
    }
}