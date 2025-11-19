using System.Diagnostics;

namespace iPath.Domain.Entities;

[DebuggerDisplay("g={GroupId}, u={UserId}")]
public class GroupMember : BaseEntity
{
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public eMemberRole Role { get; set; }


    // User Preferences
    public bool IsFavourite { get; set; }
    public eNotification Notifications { get; set; }

}
