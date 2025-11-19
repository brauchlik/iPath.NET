
namespace iPath.EF.Core.FeatureHandlers.Users;

public class GetRolesQueryHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<GetRolesQuery, Task<IEnumerable<RoleDto>>>
{
    public async Task<IEnumerable<RoleDto>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        return await db.Roles.Select(r => new RoleDto(r.Id, r.Name)).ToListAsync(cancellationToken);
    }
}