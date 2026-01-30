namespace iPath.Application.Features;

public record AssignQuestionnaireCommand(Guid Id, eQuestionnaireUsage Usage, int Priority,
    bool remove, Guid? GroupId = null, Guid? CommunityId = null)
    : IEventInput
    , IRequest<AssignQuestionnaireCommand, Task>
{
    public string ObjectName => nameof(Group);
}
