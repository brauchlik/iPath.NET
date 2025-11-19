using iPath.Application.Features.Users;
using iPath.EF.Core.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace iPath.API.Services;

public sealed class UserSession(iPathDbContext db, UserManager<User> um, IMemoryCache cache, IHttpContextAccessor acc)
    : IUserSession
{
    private SessionUserDto? _User;
    public SessionUserDto? User
    {
        get
        {
            if (_User is null)
            {
                var ctx = acc.HttpContext;
                if (ctx.User.Identity.IsAuthenticated)
                {
                    if (Guid.TryParse(ctx.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value, out var userid)){

                        var cachekey = userid.ToString();
                        if (!cache.TryGetValue(cachekey, out _User))
                        {
                            _User = LoadUser(userid).Result;

                            var opts = new MemoryCacheEntryOptions()
                                .SetSlidingExpiration(TimeSpan.FromMinutes(5));
                            cache.Set(cachekey, _User, opts);
                        }
                    }
                }
            }
            return _User;
        }
    }


    private async Task<SessionUserDto> LoadUser(Guid userid)
    {
        var user = await um.FindByIdAsync(userid.ToString());
        var roles = await um.GetRolesAsync(user);

        var groups = await db.Set<GroupMember>()
            .Where(m => m.UserId == user.Id)
            .Select(m => new UserGroupMemberDto(GroupId: m.GroupId, Groupname: m.Group.Name, Role: m.Role)).ToArrayAsync();

        return new SessionUserDto(Id: user.Id, Username: user.UserName, Email: user.Email,
            groups: groups, roles: roles.ToArray());
    }
}
