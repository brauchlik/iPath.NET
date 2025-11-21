using Microsoft.AspNetCore.Identity;

namespace iPath.EF.Core.FeatureHandlers.Users.Queries;

public class GetUserListHandler(iPathDbContext db)
    : IRequestHandler<GetUserListQuery, Task<PagedResultList<UserListDto>>>
{
    public async Task<PagedResultList<UserListDto>> Handle(GetUserListQuery request, CancellationToken cancellationToken)
    {
        var test = await db.Set<IdentityUserRole<Guid>>().ToListAsync(cancellationToken);

        // prepare query
        var q = db.Users.AsNoTracking();

        // filter
        q = q.ApplyQuery(request);

        if (!string.IsNullOrEmpty(request.SearchString))
        {
            q = q.Where(u => Microsoft.EntityFrameworkCore.EF.Functions.Like(u.UserName, $"%{request.SearchString}%") || Microsoft.EntityFrameworkCore.EF.Functions.Like(u.Email, $"%{request.SearchString}%"));
        }


        // project
        var dto = q.Select(u => new UserListDto(Id: u.Id, Username: u.UserName, 
            Email: u.Email, Initials: u.Profile.Initials,
            IsActive: u.IsActive, EmailConfirmed: u.EmailConfirmed,
            Roles: u.Roles.Select(r => r.Name).ToArray()));

        // pagination
        return await dto.ToPagedResultAsync(request, cancellationToken);
    }
}
