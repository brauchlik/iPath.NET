namespace iPath.Application.Features;

public class GetQuestionnaireListQuery : PagedQuery<QuestionnaireEntity>
    , IRequest<GetQuestionnaireListQuery, Task<PagedResultList<QuestionnaireListDto>>>
{ 
    public bool AllVersions { get; set; } 
}
