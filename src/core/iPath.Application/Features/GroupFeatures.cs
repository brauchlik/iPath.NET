using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace iPath.Application.Features;


#region "-- DTO --"
public record GroupListDto(Guid Id, string Name, eGroupVisibility Visibility, int? TotalNodes = null, int? NewNodes = null, int? NewAnnotation = null);
public record GroupDto(Guid Id, string Name, eGroupVisibility Visibility, OwnerDto Owner, GroupSettings Settings, GroupMemberDto[]? Members);

public record UserGroupMemberDto(Guid GroupId, string Groupname, eMemberRole Role);

public record GroupMemberDto(Guid UserId, string Username, eMemberRole Role);
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


    Task<GroupListDto> CreateGroupAsync(CreateGroupCommand cmd, CancellationToken ct = default);
    Task UpdateGroupAsync(UpdateGroupCommand cmd, CancellationToken ct = default);
    Task DeleteGroupAsync(DeleteGroupCommand  cmd, CancellationToken ct = default);


    Task<GroupAssignedToCommunityEvent> AssignGroupToCommunityAsync(AssignGroupToCommunityCommand cmd, CancellationToken ct = default);
    Task AssignQuestionnaireToGroupAsync(AssignQuestionnaireToGroupCommand cmd, CancellationToken ct = default);
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

public record AssignGroupToCommunityCommand(Guid GroupId, Guid CommunityId, bool Remove = false)
    : IEventInput
    // , IRequest<AssignGroupToCommunityCommand, Task<GroupAssignedToCommunityEvent>>
{
    public string ObjectName => nameof(Group);
}


public record AssignQuestionnaireToGroupCommand(Guid Id, Guid GroupId, eQuestionnaireUsage Usage)
    : IEventInput
    // , IRequest<AssignQuestionnaireToGroupCommand, Task<QuestionnaireAssignedToGroupEvent>>
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
    public Guid? CommunityId { get; set; } = null;

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
    public eGroupVisibility? Visibility { get; set; }

    public string ObjectName => nameof(Group);
}



public static class GroupExtensions
{
    public static GroupDto ToDto(this Group group)
    {
        return new GroupDto(Id: group.Id, Name: group.Name, Visibility: group.Visibility, Owner: group.Owner.ToOwnerDto(), Settings: group.Settings, 
            Members: group.Members?.Select(m => new GroupMemberDto(UserId: m.UserId, Role: m.Role, Username: m.User?.UserName)).ToArray());
    }
    
    public static GroupListDto ToListDto(this Group group)
    {
        return new GroupListDto(Id: group.Id, Name: group.Name, Visibility: group.Visibility);
    }
}