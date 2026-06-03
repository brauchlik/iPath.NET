namespace iPath.Application.Features.Notifications;

public record NotificationPayload(
    string Sender,
    string Title,
    string? AccessionNo = null,
    string? BodySite = null,
    Guid? GroupId = null
);
