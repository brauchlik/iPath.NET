namespace iPath.Application.Features;


public record QuestionnaireListDto (Guid Id, string QuestionnaireId, int Version, bool IsActive);


public record GetQuestionnaireByIdQuery(Guid Id)
    : IRequest<GetQuestionnaireByIdQuery, Task<Questionnaire>>;


public class GetQuestionnaireListQuery : PagedQuery<Questionnaire>
    , IRequest<GetQuestionnaireListQuery, Task<PagedResultList<QuestionnaireListDto>>>
{ 
    public bool AllVersions { get; set; } 
}


public record CreateQuestionnaireCommand(string QuestionnaireId, string Resource)
    : IRequest<CreateQuestionnaireCommand, Task<Guid>>;