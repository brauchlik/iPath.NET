using Microsoft.AspNetCore.Identity;
using System.Diagnostics;

namespace iPath.Domain.Entities;

// Add profile data for application users by adding properties to the ApplicationUser class

[DebuggerDisplay("User #{ipath2_id}, {UserName} {Email}")]
public class User : IdentityUser<Guid>, IBaseEntity, IHasDomainEvents
{
    public UserProfile Profile { get; private set; } = new();

    private List<GroupMember> _GroupMembership { get; set; } = new();

    public IReadOnlyCollection<GroupMember> GroupMembership => _GroupMembership;

    public GroupMember AddToGroup(Group group, eMemberRole? role = null, bool? isConsultant = false)
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
        if (isConsultant.HasValue)
        {
            ret.IsConsultant = isConsultant.Value;
        }
        return ret;
    }

    public void RemoveFromGroup(Group group)
    {
        _GroupMembership.RemoveAll(m => m.GroupId == group.Id);
    }


    private List<CommunityMember> _CommunityMembership { get; set; } = new();

    public IReadOnlyCollection<CommunityMember> CommunityMembership => _CommunityMembership;

    public CommunityMember AddToCommunity(Community community, eMemberRole? role = null, bool? isConsultant = false)
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
        if (isConsultant.HasValue){ }
        {
            ret.IsConsultant = isConsultant.Value;
        }
        return ret;
    }

    public void RemoveFromCommunity(Community community)
    {
        _CommunityMembership.RemoveAll(m => m.CommunityId == community.Id);
    }



    public ICollection<ServiceRequest> OwnedNodes { get; set; } = [];    
    public ICollection<ServiceRequestLastVisit> NodeVisitis { get; set; } = [];

    public ICollection<Role> Roles { get; set; } = [];

    public int? ipath2_id { get; set; }
    public string? ipath2_username { get; set; }
    public string? ipath2_password { get; set; }

    public bool IsActive { get; set; }

    // just a flag for admins
    public bool IsNew { get; set; }

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




    #region "-- Domain Events --"
    [JsonIgnore]
    public List<EventEntity> Events { get; set; } = new();

    public void AddEventEntity(EventEntity eventItem)
        => Events.Add(eventItem);

    public void ClearDomainEvents()
        => Events.Clear();

    public void AddUserCreatedEvent()
    {
        var e = new UserCreatedEvent
        {
            EventId = Guid.CreateVersion7(),
            EventDate = DateTime.UtcNow,
            UserId = this.Id,
            EventName = typeof(UserCreatedEvent).Name,
            ObjectName = nameof(User),
            ObjectId = this.Id,
            Payload = ""
        };
        AddEventEntity(e);
    }
    #endregion
}



public class UserCreatedEvent : EventEntity
{
}
