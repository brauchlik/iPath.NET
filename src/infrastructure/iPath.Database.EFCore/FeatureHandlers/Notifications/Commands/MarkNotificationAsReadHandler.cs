using iPath.Application.Features.Notifications;

namespace iPath.EF.Core.FeatureHandlers.Notifications;

public class MarkNotificationAsReadHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<MarkNotificationAsReadCommand, Task<bool>>
{
    public async Task<bool> Handle(MarkNotificationAsReadCommand request, CancellationToken ct)
    {
        var notification = await db.NotificationQueue.FindAsync(new object[] { request.NotificationId }, ct);
        if (notification is null)
            return false;

        if (notification.UserId != sess.User?.Id && !sess.IsAdmin)
            throw new NotAllowedException();

        notification.MarkAsRead();
        await db.SaveChangesAsync(ct);
        return true;
    }
}
