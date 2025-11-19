using System.ComponentModel.DataAnnotations;

namespace iPath.Application.Features;

public record GroupListDto(Guid Id, string Name, int? TotalNodes = null, int? NewNodes = null, int? NewAnnotation = null);
public record GroupDto(Guid Id, string Name, GroupSettings Settings, GroupMemberDto[]? Members);

public record UserGroupMemberDto(Guid GroupId, string Groupname, eMemberRole Role);

public record GroupMemberDto(Guid UserId, string Username, eMemberRole Role);



#region "-- Queries --"
public class GetGroupListQuery : PagedQuery<GroupListDto>
    , IRequest<GetGroupListQuery, Task<PagedResultList<GroupListDto>>>
{
    public bool IncludeCounts { get; set; }
    public bool AdminList { get; set; }
    public string SearchString { get; set; }
}

public record GetGroupByIdQuery(Guid GroupId) : IRequest<GetGroupByIdQuery, Task<GroupDto>>;
#endregion


#region "-- Commands --"
public record AssignGroupToCommunityCommand(Guid GroupId, Guid CommunityId, bool Remove = false)
    : IRequest<AssignGroupToCommunityCommand, Task<GroupAssignedToCommunityEvent>>, IEventInput
{
    public string ObjectName => nameof(Group);
}


public record AssignQuestionnaireToGroupCommand(Guid Id, Guid GroupId, eQuestionnaireUsage Usage)
    : IRequest<AssignQuestionnaireToGroupCommand, Task<QuestionnaireAssignedToGroupEvent>>
    , IEventInput
{
    public string ObjectName => nameof(Group);
}


public record CreateGroupCommand : IRequest<CreateGroupCommand, Task<Group>>, IEventInput
{
    [MinLength(4)]
    public string Name { get; init; }
    public string? Purpose { get; init; } = null;
    public eGroupVisibility? Visibility { get; init; } = null;

    public Guid OwnerId { get; init; }
    public Guid? CommunityId { get; init; } = null;

    public string ObjectName => nameof(Group);
}

public record DeleteGroupCommand(Guid Id) : IRequest<DeleteGroupCommand, Task<Guid>>, IEventInput
{
    public string ObjectName => nameof(Group);
}

public class UpdateGroupCommand : IRequest<UpdateGroupCommand, Task<Group>>, IEventInput
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public GroupSettings? Settings { get; set; } = null;

    public Guid? OwnerId { get; set; }

    public string ObjectName => nameof(Group);
}


#endregion



#region "-- Commands --"
public class GroupAssignedToCommunityEvent : EventEntity;
public class QuestionnaireAssignedToGroupEvent : EventEntity { }

public class GroupCreatedEvent : EventEntity;
public class GroupDeletedEvent : EventEntity;
public class GroupUpdatedEvent : EventEntity;
#endregion