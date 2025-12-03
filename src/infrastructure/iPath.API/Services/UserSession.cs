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
    private SessionUserDto? _user;
    public SessionUserDto? User
    {
        get
        {
            if (_user is null)
            {
                var ctx = acc.HttpContext;
                if (ctx.User.Identity.IsAuthenticated)
                {
                    if (Guid.TryParse(ctx?.User?.Claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value, out var userid)){

                        var cachekey = userid.ToString();
                        if (!cache.TryGetValue(cachekey, out _user))
                        {
                            _user = LoadUser(userid).Result;

                            var opts = new MemoryCacheEntryOptions()
                                .SetSlidingExpiration(TimeSpan.FromMinutes(5));
                            cache.Set(cachekey, _user, opts);
                        }
                    }
                }
            }
            return _user;
        }
    }

    public void ReloadUser(Guid userId)
    {
        cache.Remove(userId);
    }

    private async Task<SessionUserDto?> LoadUser(Guid userid)
    {
        var user = await um.FindByIdAsync(userid.ToString());
        if (user is null) return null;
        var roles = await um.GetRolesAsync(user);

        var groups = await db.Set<GroupMember>()
            .Where(m => m.UserId == user.Id)
            .ToDictionaryAsync(m => m.GroupId, m => m.Role);

        var communities = await db.Set<CommunityMember>()
            .Where(m => m.UserId == user.Id)
            .ToDictionaryAsync(m => m.CommunityId, m => m.Role);

        return new SessionUserDto(Id: user.Id, Username: user.UserName, Email: user.Email, Initials: user.Profile?.Initials,
            groups: groups, communities: communities, roles: roles.ToArray());
    }
}
