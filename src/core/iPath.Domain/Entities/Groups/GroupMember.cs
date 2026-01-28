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

    public bool IsConsultant { get; set; }


    public eNotificationSource NotificationSource { get; set; } = eNotificationSource.None;
    public eNotificationTarget NotificationTarget { get; set; } = eNotificationTarget.None;

    public NotificationSettings? NotificationSettings { get; set; } = new();
}


public class NotificationSettings
{
    public bool DailyEmailSummary { get; set; }
    public TimeOnly? SummaryEmailTime { get; set; }

    public bool UseProfileBodySiteFilter { get; set; }
    public ConceptFilter? BodySiteFilter { get; set; }
}



[Flags]
public enum eNotificationSource
{
    None = 0,
    NewCase = 1,
    NewAnnotation = 2,
    NewAnnotationOnMyCase = 4
}

