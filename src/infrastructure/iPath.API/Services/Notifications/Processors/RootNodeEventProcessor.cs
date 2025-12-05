using iPath.Domain.Notificxations;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;

namespace iPath.API.Services.Notifications.Processors;


/*
 * This "Processor" is responsible for filtering NodeEvents and then transofrm them into
 * notifications. Based on the event, it has to read all user subscriptions. User subscriptions
 * are currently stored on the groupmember table on contain a filter for source (abstract event type)
 * and the desired target (in app, email, ...)
 * 
 * The processed notifications are stored in the database and placed on the notification queue
 * for transmission.
 * 
 */


public class RootNodeEventProcessor(iPathDbContext db, INotificationQueue queue) : INodeEventProcessor
{
    public async Task ProcessEvent(NodeEvent evt, CancellationToken ct)
    {

        // we only process root nodes here => nodes that have GroupId
        if (!evt.Node.GroupId.HasValue) return;

        // find all subscriptions for this group (active users only)
        var subscriptions = await db.Set<GroupMember>()
            .Include(m => m.User)
            .AsNoTracking()
            .Where(m => m.User.IsActive)
            .Where(m => m.GroupId == evt.Node.GroupId && m.NotificationSource != eNotificationSource.None)
            .ToListAsync(ct);

        // Filter by Notification Source
        foreach (var s in subscriptions)
        {
            // do not process users own events
            if (evt.UserId != s.UserId)
            {
                // Annotation Events
                if (evt is AnnotationCreatedEvent)
                {
                    // For NewAnnotationOnMyCase => filter by case owner 
                    if (s.NotificationSource.HasFlag(eNotificationSource.NewAnnotationOnMyCase))
                    {
                        if (evt.Node.OwnerId == s.UserId)
                        {
                            await Enqueue(eNodeNotificationType.NewAnnotation, evt, s, ct);
                        }
                    }
                    else if (s.NotificationSource.HasFlag(eNotificationSource.NewAnnotation))
                    {
                        await Enqueue(eNodeNotificationType.NewAnnotation, evt, s, ct);
                    }
                }
                else if (evt is RootNodePublishedEvent)
                {
                    if (s.NotificationSource.HasFlag(eNotificationSource.NewCase))
                    {
                        await Enqueue(eNodeNotificationType.NodePublished, evt, s, ct);
                    }
                }
            }
        }
    }


    // Rules by notification target
    protected async Task Enqueue(eNodeNotificationType t, NodeEvent evt, GroupMember m, CancellationToken ct)
    {
        if (m.NotificationTarget.HasFlag(eNotificationTarget.InApp))
        {
            // => SignalR
            await Enqueue(t, evt, eNotificationTarget.InApp, false, m.UserId, ct);
        }
        else if (m.NotificationTarget.HasFlag(eNotificationTarget.Email))
        {
            bool daily = m.NotificationSettings is not null && m.NotificationSettings.DailyEmailSummary;
            await Enqueue(t, evt, eNotificationTarget.Email, false, m.UserId, ct);
        }
    }

    protected async Task Enqueue(eNodeNotificationType t, NodeEvent n, eNotificationTarget target, bool dailySummary, Guid ReceiverId, CancellationToken ct)
    {
        var entity = Notification.Create(t, target, false, ReceiverId, n);
        await db.NotificationQueue.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);
        await queue.EnqueueAsync(entity);
    }
}
