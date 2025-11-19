using Microsoft.AspNetCore.Identity;

namespace iPath.EF.Core.FeatureHandlers.Users.Queries;

public class GetUserByIdHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<GetUserByIdQuery, Task<UserDto>>
{
    public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .Select(u => new UserDto
            {
                Id = u.Id,
                CreatedOn = u.CreatedOn,
                Username = u.UserName,
                Email = u.Email,
                Profile = u.Profile,
                Roles = u.Roles.Select(r => r.Name).ToArray()                
            })
            .FirstOrDefaultAsync(u => u.Id == request.Id);

        return user;
    }
}
