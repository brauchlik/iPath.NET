using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;

namespace iPath.Domain.Entities;

// Add profile data for application users by adding properties to the ApplicationUser class

[DebuggerDisplay("User #{ipath2_id}, {UserName} {Email}")]
public class User : IdentityUser<Guid>, IBaseEntity
{
    public UserProfile Profile { get; private set; } = new();

    private List<GroupMember> _GroupMembership { get; set; } = new();
    public IReadOnlyCollection<GroupMember> GroupMembership => _GroupMembership;
    public GroupMember AddToGroup(Group group, eMemberRole? role = null)
    {
        var ret = _GroupMembership.FirstOrDefault(m => m.GroupId == group.Id);
        if (ret is null)
        {
            ret = new GroupMember { User = this, Group = group };
            _GroupMembership.Add(ret);
        }
        if (role.HasValue)
        {
            ret.Role = role.Value;
        }
        return ret;
    }
    public void RemoveFromGroup(Group group)
    {
        _GroupMembership.RemoveAll(m => m.GroupId == group.Id);
    }


    private List<CommunityMember> _CommunityMembership { get; set; } = new();
    public IReadOnlyCollection<CommunityMember> CommunityMembership => _CommunityMembership;
    public CommunityMember AddToCommunity(Community community, eMemberRole? role = null)
    {
        var ret = _CommunityMembership.FirstOrDefault(m => m.CommunityId == community.Id);
        if (ret is null)
        {
            ret = new CommunityMember { User = this, Community = community };
            _CommunityMembership.Add(ret);
        }
        if (role.HasValue)
        {
            ret.Role = role.Value;
        }
        return ret;
    }
    public void RemoveFromCommunity(Community community)
    {
        _CommunityMembership.RemoveAll(m => m.CommunityId == community.Id);
    }



    public ICollection<Node> OwnedNodes { get; set; } = [];    
    public ICollection<NodeLastVisit> NodeVisitis { get; set; } = [];

    public ICollection<Role> Roles { get; set; } = [];

    public int? ipath2_id { get; set; }
    public string? ipath2_username { get; set; }
    public string? ipath2_password { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedOn { get; set; }

    public void UpdateProfile(UserProfile profile)
    {
        Profile = profile;
        ModifiedOn = DateTime.UtcNow;
    }

    public void UpdateActive(bool active)
    {
        IsActive = active;
        ModifiedOn = DateTime.UtcNow;
    }
}

