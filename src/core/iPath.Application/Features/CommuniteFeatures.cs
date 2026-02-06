using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace iPath.Application.Features;


#region "-- Queries --"

public record CommunityListDto(Guid Id, string Name, OwnerDto? Owner = null, CommunitySettings? Settings = null);

public record CommunityDto(Guid Id, string Name, CommunitySettings Settings, eCommunityVisibility Visibility,
    OwnerDto? Owner, 
    GroupListDto[] Groups,
    GroupListDto[]? ExtraGroups,
    QuestionnaireForCommunityDto[]? Questionnaires);


public class GetCommunityListQuery : PagedQuery<CommunityListDto>
    , IRequest<GetCommunityListQuery, Task<PagedResultList<CommunityListDto>>>
{
}

public record GetCommunityByIdQuery(Guid id) 
    : IRequest<GetCommunityByIdQuery, Task<CommunityDto>>;


public class GetCommunityMembersQuery : PagedQuery<CommunityMemberDto> 
    , IRequest<GetCommunityMembersQuery, Task<PagedResultList<CommunityMemberDto>>>
{
    public Guid CommunityId { get; set; }
}


public record QuestionnaireForCommunityDto(Guid qId, string QuestinnaireId,
    string QuestinnaireName,
    eQuestionnaireUsage Usage,
    QuestionnaireSettings Settings,
    int Priority,
    int? ExplicitVersion = null)
{
    public override string ToString() => QuestinnaireName;
}

#endregion


#region "-- Commands --"

public record CreateCommunityCommand(
    [Required, MinLength(4)]
    string Name,
    Guid OwnerId,
    eCommunityVisibility? Visibility = eCommunityVisibility.MembersOnly,
    string? Description = null,
    string? BaseUrl = null)
    : IRequest<CreateCommunityCommand, Task<CommunityListDto>>, IEventInput
{
    public string ObjectName => "Community";
}

public record UpdateCommunityCommand(
    Guid Id,
    string? Name,
    Guid? OwnerId,
    eCommunityVisibility? Visibility = null,
    CommunitySettings? Settings = null)
    : IRequest<UpdateCommunityCommand, Task<CommunityListDto>>, IEventInput
{
    public string ObjectName => "Community";
}

public record DeleteCommunityCommand(Guid Id)
    : IRequest<DeleteCommunityCommand, Task<Guid>>, IEventInput
{
    public string ObjectName => "Community";
}

#endregion



public static class CommunityExtensions
{
    public static CommunityListDto? ToListDto(this Community? entity)
        => entity is null ? null : new CommunityListDto(Id: entity.Id, Name: entity.Name, Owner: entity.Owner.ToOwnerDto(), Settings: entity.Settings);

    public static CommunityListDto? ToListDto(this CommunityDto? dto)
        => dto is null ? null : new CommunityListDto(Id: dto.Id, Name: dto.Name, Owner: dto.Owner);    
}




