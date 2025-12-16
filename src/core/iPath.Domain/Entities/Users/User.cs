using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;

namespace iPath.Domain.Entities;

// Add profile data for application users by adding properties to the ApplicationUser class

[DebuggerDisplay("User #{ipath2_id}, {UserName} {Email}")]
public class User : IdentityUser<Guid>, IBaseEntity
{
    public UserProfile Profile { get; private set; } = new();

    public ICollection<GroupMember> GroupMembership { get; set; } = [];
    public ICollection<CommunityMember> CommunityMembership { get; set; } = [];

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

