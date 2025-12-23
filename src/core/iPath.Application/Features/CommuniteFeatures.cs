using System.ComponentModel.DataAnnotations;

namespace iPath.Application.Features;


#region "-- Queries --"

public record CommunityListDto(Guid Id, string Name, CommunitySettings? Settings = null);
public record CommunityDto(Guid Id, string Name, CommunitySettings Settings, eCommunityVisibility Visibility, GroupListDto[] Groups, OwnerDto? Owner);


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
    public static CommunityListDto ToListDto(this Community entity)
        => new CommunityListDto(Id: entity.Id, Name: entity.Name);
}