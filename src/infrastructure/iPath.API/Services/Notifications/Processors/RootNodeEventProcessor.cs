using iPath.Domain.Notificxations;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;

namespace iPath.API.Services.Notifications.Processors;

public class RootNodeEventProcessor(iPathDbContext db, INotificationQueue queue) : INodeEventProcessor
{
    public async Task ProcessEvent(NodeNofitication n, CancellationToken ct)
    {
        // we only process root nodes here => nodes that have GroupId
        if (!n.GroupId.HasValue) return;

        // find all subscriptions for this group (active users only)
        var subscriptions = await db.Set<GroupMember>()
            .Include(m => m.User)
            .AsNoTracking()
            .Where(m => m.User.IsActive)
            .Where(m => m.GroupId == n.GroupId && m.NotificationSource != eNotificationSource.None)
            .ToListAsync(ct);

        // Filter by Notification Source
        foreach (var s in subscriptions)
        {
            // do not process users own events
            if (n.UserId != s.UserId)
            {
                // Annotation Events
                if (n.type == eNodeEventType.NewAnnotation)
                {
                    // For NewAnnotationOnMyCase => filter by case owner 
                    if (s.NotificationSource.HasFlag(eNotificationSource.NewAnnotationOnMyCase))
                    {
                        if (n.OwnerId.HasValue && n.OwnerId.Value == s.UserId)
                        {
                            await Enqueue(n, s, ct);
                        }
                    }
                    else if (s.NotificationSource.HasFlag(eNotificationSource.NewAnnotation))
                    {
                        await Enqueue(n, s, ct);
                    }
                }
                else if (n.type == eNodeEventType.NodePublished)
                {
                    if (s.NotificationSource.HasFlag(eNotificationSource.NewCase))
                    {
                        await Enqueue(n, s, ct);
                    }
                }
            }
        }
    }

    protected async Task Enqueue(NodeNofitication n, GroupMember m, CancellationToken ct)
    {
        if (m.NotificationTarget.HasFlag(eNotificationTarget.InApp))
        {
            // => SignalR
            await Enqueue(n, eNotificationTarget.InApp, false, ct);
        }
        else if (m.NotificationTarget.HasFlag(eNotificationTarget.Email))
        {
            bool daily = m.NotificationSettings is not null && m.NotificationSettings.DailyEmailSummary;
            await Enqueue(n, eNotificationTarget.Email, false, ct);
        }
    }

    protected async Task Enqueue(NodeNofitication n, eNotificationTarget target, bool dailySummary, CancellationToken ct)
    {
        var entity = Notification.Create(n.type, eNotificationTarget.InApp, false, n);
        await db.NotificationQueue.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);
        await queue.EnqueueAsync(entity);
    }
}
