using iPath.Application.Features.Users;
using iPath.EF.Core.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace iPath.API.Services;

public sealed class UserSession(iPathDbContext db, UserManager<User> um, IMemoryCache cache, IHttpContextAccessor acc, ILogger<UserSession> logger)
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
                if (ctx is null)
                {
                    // no http context => Server Side Worker
                    _user = SessionUserDto.Server;
                }
                else if (ctx.User.Identity?.IsAuthenticated == true)
                {
                    Guid? userid = null;
                    if (Guid.TryParse(ctx.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value, out var parsedId) && parsedId != Guid.Empty)
                    {
                        userid = parsedId;
                    }

                    if (userid.HasValue)
                    {
                        var cachekey = userid.Value.ToString();
                        if (!cache.TryGetValue(cachekey, out var cachedUser))
                        {
                            cachedUser = LoadUser(userid.Value).Result;

                            var opts = new MemoryCacheEntryOptions()
                                .SetSlidingExpiration(TimeSpan.FromMinutes(5));
                            cache.Set(cachekey, cachedUser, opts);
                        }
                        _user = (SessionUserDto?)cachedUser;
                    }

                    if (ctx.User.IsInRole("CaseRoomGuest"))
                    {
                        if (_user is null || _user.Id == Guid.Empty)
                        {
                            _user = new SessionUserDto(Guid.Empty, "Guest", "", "", ["CaseRoomGuest"], new Dictionary<Guid, eMemberRole>(), new List<UserGroupMemberDto>());
                        }
                        else if (!_user.roles.Contains("CaseRoomGuest"))
                        {
                            var newRoles = _user.roles.ToList();
                            newRoles.Add("CaseRoomGuest");
                            _user = _user with { roles = newRoles.ToArray() };
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
        User? user = null;
        try
        {
            user = await um.FindByIdAsync(userid.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cannot load user {0}", userid);
        }

        if (user is null) return SessionUserDto.Anonymous;

        var roles = await um.GetRolesAsync(user);

        var groups = await db.Set<GroupMember>()
            .Where(m => m.UserId == user.Id && m.Role != eMemberRole.Banned)
            .Select(m => new UserGroupMemberDto(m.GroupId, m.Group.Name, m.Role, m.IsConsultant))
            .ToListAsync();
            // .ToDictionaryAsync(m => m.GroupId, m => m.ToD);

        var communities = await db.Set<CommunityMember>()
            .Where(m => m.UserId == user.Id && m.Role != eMemberRole.Banned)
            .ToDictionaryAsync(m => m.CommunityId, m => m.Role);

        return new SessionUserDto(Id: user.Id, Username: user.UserName, Email: user.Email, Initials: user.Profile?.Initials,
            groups: groups, communities: communities, roles: roles.ToArray());
    }
}
