namespace iPath.Domain.Entities;

public class CommunityMember : BaseEntity
{

    public Guid CommunityId { get; set; }
    public Community Community { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public eMemberRole Role { get; set; }
    public bool AllGroups { get; set; }

    public int? iPath2_Id { get; set; }
}
