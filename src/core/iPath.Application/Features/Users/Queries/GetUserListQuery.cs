namespace iPath.Application.Features.Users;

public class GetUserListQuery : PagedQuery<UserListDto>
    , IRequest<GetUserListQuery, Task<PagedResultList<UserListDto>>>
{
    public string SearchString { get; set; } = "";
}