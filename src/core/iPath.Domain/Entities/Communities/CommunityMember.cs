namespace iPath.Domain.Entities;

public class CommunityMember : BaseEntity
{

    public Guid CommunityId { get; init; }
    public Community Community { get; init; } = null!;

    public Guid UserId { get; init; }
    public User User { get; init; } = null!;

    public eMemberRole Role { get; set; }
    public bool AllGroups { get; set; }

    public bool IsConsultant { get; set; }

    public int? iPath2_Id { get; init; }
}
