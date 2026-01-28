namespace iPath.Application.Features.Notifications;

public static class NotificationExtensions
{
    extension(Notification n)
    {
        public NotificationDto ToDto()
        {
            return new NotificationDto(Id: n.Id, Date: n.CreatedOn, EventType: n.EventType, Target: n.Target, Receiver: n.User.ToOwnerDto(), Payload: n.Data);
        }
    }
}
