using iPath.Application.Features.Notifications;
using iPath.Domain.Notificxations;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
 * Filtering logic is delegated to INotificationFilterService for testability.
 * 
 */


public class ServiceRequestEventProcessor(
    iPathDbContext db,
    ILogger<ServiceRequestEventProcessor> logger,
    INotificationQueue queue,
    INotificationFilterService filter)
    : IServiceRequestEventProcessor
{
    public async Task ProcessEvent(ServiceRequestEvent evt, CancellationToken ct)
    {
        if (evt is IEventWithNotifications)
        {
            var subscriptions = await db.Set<GroupMember>()
                .Include(m => m.User)
                .AsNoTracking()
                .Where(m => m.User.IsActive)
                .Where(m => m.GroupId == evt.ServiceRequest.GroupId && m.NotificationSource != eNotificationSource.None)
                .ToListAsync(ct);

            foreach (var s in subscriptions)
            {
                var result = filter.ShouldNotify(s, evt, evt.ServiceRequest.OwnerId);
                
                if (!result.ShouldNotify)
                {
                    if (!string.IsNullOrEmpty(result.SkipReason))
                    {
                        logger.LogDebug("Notification skipped for user {UserId}: {Reason}", s.UserId, result.SkipReason);
                    }
                    continue;
                }

                logger.LogInformation("Processing {EventName} on request {RequestId} for user {UserId}", 
                    evt.EventName, evt.ServiceRequest.Id, s.UserId);
                
                await Enqueue(result.NotificationType!.Value, evt, s, ct);
            }
        }
    }


    // Rules by notification target
    protected async Task Enqueue(eNodeNotificationType t, ServiceRequestEvent evt, GroupMember m, CancellationToken ct)
    {
        if (m.NotificationTarget.HasFlag(eNotificationTarget.InApp))
        {
            // => SignalR
            await Enqueue(t, evt, eNotificationTarget.InApp, false, m.UserId, ct);
        }
        if (m.NotificationTarget.HasFlag(eNotificationTarget.Email))
        {
            bool daily = m.NotificationSettings is not null && m.NotificationSettings.DailyEmailSummary;
            await Enqueue(t, evt, eNotificationTarget.Email, false, m.UserId, ct);
        }
    }

    protected async Task Enqueue(eNodeNotificationType t, ServiceRequestEvent evt, eNotificationTarget target, bool dailySummary, Guid ReceiverId, CancellationToken ct)
    {
        try
        {
            var entity = Notification.Create(t, target, false, ReceiverId, evt.ServiceRequest.Id, evt.EventId);
            await db.NotificationQueue.AddAsync(entity, ct);
            await db.SaveChangesAsync(ct);
            // enque for publishing
            await queue.EnqueueAsync(entity.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
        }
    }
}
