using iPath.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace iPath.Application.Features;


#region "-- Queries --"

public record CommunityListDto(Guid Id, string Name);
public record CommunityDto(Guid Id, string Name, string Description, string BaseUrl, GroupListDto[] Groups, OwnerDto? Owner);


public class GetCommunityListQuery : PagedQuery<CommunityListDto>
    , IRequest<GetCommunityListQuery, Task<PagedResultList<CommunityListDto>>>
{
}

public record GetCommunityByIdQuery(Guid id) 
    : IRequest<GetCommunityByIdQuery, Task<CommunityDto>>;


#endregion


#region "-- Commands --"

public record CreateCommunityInput(
    [Required, MinLength(4)]
    string Name,
    Guid OwnerId,
    string? Description = null,
    string? BaseUrl = null)
    : IRequest<CreateCommunityInput, Task<CommunityListDto>>, IEventInput
{
    public string ObjectName => "Community";
}

public record UpdateCommunityInput(
    Guid Id,
    string? Name,
    Guid? OwnerId,
    string? Description = null,
    string? BaseUrl = null)
    : IRequest<UpdateCommunityInput, Task<CommunityListDto>>, IEventInput
{
    public string ObjectName => "Community";
}

public record DeleteCommunityInput(Guid Id)
    : IRequest<DeleteCommunityInput, Task<Guid>>, IEventInput
{
    public string ObjectName => "Community";
}

#endregion



public static class CommunityExtensions
{
    public static CommunityListDto ToListDto(this Community entity)
        => new CommunityListDto(Id: entity.Id, Name: entity.Name);
}