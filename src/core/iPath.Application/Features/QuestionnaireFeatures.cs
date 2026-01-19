namespace iPath.Application.Features;


public record QuestionnaireListDto (Guid Id, string QuestionnaireId, string Name, int Version, bool IsActive);


public record GetQuestionnaireByIdQuery(Guid Id)
    : IRequest<GetQuestionnaireByIdQuery, Task<QuestionnaireEntity>>;


public record GetQuestionnaireQuery(string QuestionnaireId, int? Version)
    : IRequest<GetQuestionnaireQuery, Task<QuestionnaireEntity>>;


public class GetQuestionnaireListQuery : PagedQuery<QuestionnaireEntity>
    , IRequest<GetQuestionnaireListQuery, Task<PagedResultList<QuestionnaireListDto>>>
{ 
    public bool AllVersions { get; set; } 
}


public record AssignQuestionnaireCommand(Guid Id, eQuestionnaireUsage Usage, bool remove, Guid? GroupId = null, Guid? CommunityId = null)
    : IEventInput
    , IRequest<AssignQuestionnaireCommand, Task>
{
    public string ObjectName => nameof(Group);
}



public record UpdateQuestionnaireCommand(string QuestionnaireId, string Name, string Resource, bool insert)
    : IRequest<UpdateQuestionnaireCommand, Task<Guid>>;