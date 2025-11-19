using DispatchR.Abstractions.Send;

namespace iPath.Shared.Features;

internal class UserQueries { }


public record UserListDto(Guid Id, string Username, string Email, string[] Roles);

public class UserListQuery : PagedQuery<UserListDto>
    , IRequest<UserListQuery, Task<PagedResult<UserListDto>>>
{ 
}