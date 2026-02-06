using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace iPath.Application.Features;


#region "-- DTO --"
public record GroupListDto(Guid Id, string Name, eGroupVisibility Visibility,
    int? TotalRequests = null, int? NewRequests = null, int? NewAnnotation = null);

public record GroupDto(Guid Id, string Name, eGroupVisibility Visibility, 
    OwnerDto Owner,
    CommunityListDto? Community,
    GroupSettings Settings,    
    GroupMemberDto[]? Members,
    CommunityListDto[]? ExtraCommunities,
    QuestionnaireForGroupDto[]? Questionnaires);


public record UserGroupMemberDto(Guid GroupId, string Groupname, eMemberRole Role, bool IsConsultant);

public record GroupMemberDto(Guid UserId, string Username, eMemberRole Role, bool IsConsultant);


public record QuestionnaireForGroupDto(Guid qId, string QuestinnaireId, 
    string QuestinnaireName, 
    eQuestionnaireUsage Usage, 
    QuestionnaireSettings Settings, 
    int Priority,
    int? ExplicitVersion = null)
{
    public override string ToString() => QuestinnaireName;
}

#endregion


#region "-- event --"
public class GroupAssignedToCommunityEvent : EventEntity;
public class QuestionnaireAssignedToGroupEvent : EventEntity { }

public class GroupCreatedEvent : EventEntity;
public class GroupDeletedEvent : EventEntity;
public class GroupUpdatedEvent : EventEntity;
#endregion


public interface IGroupService
{
    Task<PagedResultList<GroupListDto>> GetGroupListAsync(GetGroupListQuery query, CancellationToken ct = default);
    Task<GroupDto> GetGroupByIdAsync(Guid GroupId, CancellationToken ct = default);
    Task<PagedResultList<GroupMemberDto>> GetGroupMembersAsync(GetGroupMembersQuery query, CancellationToken ct = default);


    Task<GroupListDto> CreateGroupAsync(CreateGroupCommand cmd, CancellationToken ct = default);
    Task UpdateGroupAsync(UpdateGroupCommand cmd, CancellationToken ct = default);
    Task DeleteGroupAsync(DeleteGroupCommand  cmd, CancellationToken ct = default);
    Task DeleteGroupDraftsAsync(Guid groupId, CancellationToken ct = default);


    Task<GroupAssignedToCommunityEvent> AssignGroupToCommunityAsync(AssignGroupToCommunityCommand cmd, CancellationToken ct = default);
}




public class GetGroupListQuery : PagedQuery<GroupListDto>
    // , IRequest<GetGroupListQuery, Task<PagedResultList<GroupListDto>>>
{
    public bool IncludeCounts { get; set; }
    public bool AdminList { get; set; }
    public Guid? CommunityId {  get; set; }
    public string SearchString { get; set; }
}

public record GetGroupByIdQuery(Guid GroupId) : IRequest<GetGroupByIdQuery, Task<GroupDto>>;



public class GetGroupMembersQuery : PagedQuery<GroupMemberDto>
// , IRequest<GetGroupListQuery, Task<PagedResultList<GroupListDto>>>
{
    required public Guid GroupId { get; init; }
    public string SearchString { get; set; }
}





public record AssignGroupToCommunityCommand(Guid GroupId, Guid CommunityId, bool Remove = false)
    : IEventInput
    // , IRequest<AssignGroupToCommunityCommand, Task<GroupAssignedToCommunityEvent>>
{
    public string ObjectName => nameof(Group);
}




public class CreateGroupCommand : IRequest<CreateGroupCommand, Task<GroupListDto>>, IEventInput
{
    [MinLength(3)]
    public string Name { get; set; }
    public GroupSettings Settings { get; set; } = new(); 
    public eGroupVisibility? Visibility { get; set; }

    public Guid OwnerId { get; set; }
    public Guid CommunityId { get; set; }

    [JsonIgnore]
    public string ObjectName => nameof(Group);
}

public record DeleteGroupCommand(Guid Id)
    // : IRequest<DeleteGroupCommand, Task<Guid>>, IEventInput
{
    public string ObjectName => nameof(Group);
}


public class UpdateGroupCommand
    // : IRequest<UpdateGroupCommand, Task>, IEventInput
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public GroupSettings? Settings { get; set; } = null;

    public Guid? OwnerId { get; set; }
    public Guid? CommunityId { get; set; }

    public eGroupVisibility? Visibility { get; set; }

    public string ObjectName => nameof(Group);
}



public static class GroupExtensions
{
    public static GroupDto ToDto(this Group group)
    {
        return new GroupDto(Id: group.Id, Name: group.Name, Visibility: group.Visibility, 
            Owner: group.Owner.ToOwnerDto(),
            Community: group.Community.ToListDto(),
            Settings: group.Settings,
            Members: group.Members?.Select(m => new GroupMemberDto(UserId: m.UserId, Role: m.Role, IsConsultant: m.IsConsultant, Username: m.User?.UserName)).ToArray(),
            ExtraCommunities: group.ExtraCommunities.Select(c => new CommunityListDto(Id: c.Community.Id, Name: c.Community.Name)).ToArray(),
            Questionnaires: group.Quesionnaires.Select(q => new QuestionnaireForGroupDto(qId: q.QuestionnaireId, 
            QuestinnaireId: q.Questionnaire.QuestionnaireId, 
            QuestinnaireName: q.Questionnaire.Name, 
            Usage: q.Usage, 
            Settings: q.Questionnaire.Settings,
            Priority: q.Priority,
            ExplicitVersion: q.ExplicitVersion)).ToArray());
    }
    
    public static GroupListDto ToListDto(this Group group)
    {
        return new GroupListDto(Id: group.Id, Name: group.Name, Visibility: group.Visibility);
    }
}


