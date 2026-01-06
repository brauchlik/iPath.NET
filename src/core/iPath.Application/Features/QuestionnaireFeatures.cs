namespace iPath.Application.Features;


public record QuestionnaireListDto (Guid Id, string QuestionnaireId, string Name, int Version, bool IsActive);


public record GetQuestionnaireByIdQuery(Guid Id)
    : IRequest<GetQuestionnaireByIdQuery, Task<Questionnaire>>;


public record GetQuestionnaireQuery(string QuestionnaireId, int? Version)
    : IRequest<GetQuestionnaireQuery, Task<Questionnaire>>;


public class GetQuestionnaireListQuery : PagedQuery<Questionnaire>
    , IRequest<GetQuestionnaireListQuery, Task<PagedResultList<QuestionnaireListDto>>>
{ 
    public bool AllVersions { get; set; } 
}


public record CreateQuestionnaireCommand(string QuestionnaireId, string Name, string Resource)
    : IRequest<CreateQuestionnaireCommand, Task<Guid>>;