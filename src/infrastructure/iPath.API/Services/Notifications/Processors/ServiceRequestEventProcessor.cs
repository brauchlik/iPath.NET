using iPath.Application.Coding;
using iPath.Domain.Notificxations;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
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
 */


public class ServiceRequestEventProcessor(
    iPathDbContext db,
    ILogger<ServiceRequestEventProcessor> logger,
    INotificationQueue queue,
    [FromKeyedServices("icdo")] CodingService coding)
    : IServiceRequestEventProcessor
{
    public async Task ProcessEvent(ServiceRequestEvent evt, CancellationToken ct)
    {
        if (evt is IEventWithNotifications)
        {
            // find all subscriptions for this group (active users only)
            var subscriptions = await db.Set<GroupMember>()
                .Include(m => m.User)
                .AsNoTracking()
                .Where(m => m.User.IsActive)
                .Where(m => m.GroupId == evt.ServiceRequest.GroupId && m.NotificationSource != eNotificationSource.None)
                .ToListAsync(ct);


            // Filter by Notification Source
            foreach (var s in subscriptions)
            {
                // do not process users own events
                if (evt.UserId != s.UserId)
                {
                    // BodySite Filter
                    ConceptFilter f = s.NotificationSettings?.BodySiteFilter;
                    if (s.NotificationSettings.UseProfileBodySiteFilter)
                    {
                        f = s.User.Profile?.SpecialisationBodySite;
                    }

                    if (!IsValidBodySite(evt.ServiceRequest, f))
                    {
                        logger.LogInformation("Notifications {1} on {2} for {3} Bodysite Filter skipped ", evt.EventName, evt.ServiceRequest.Id, s.User.UserName);
                        logger.LogInformation(" - Bodysite {1} not in {2}", evt.ServiceRequest.Description.BodySite?.Code, f?.ConceptCodesString);
                    }
                    else
                    {
                        logger.LogInformation("Processing {1} on request {2} for {3}", evt.EventName, evt.ServiceRequest.Id, s.User.UserName);
                        // Annotation Events
                        if (evt is AnnotationCreatedEvent)
                        {
                            // For NewAnnotationOnMyCase => filter by case owner 
                            if (s.NotificationSource.HasFlag(eNotificationSource.NewAnnotationOnMyCase) && (evt.ServiceRequest.OwnerId == s.UserId))
                            {
                                await Enqueue(eNodeNotificationType.NewAnnotation, evt, s, ct);
                            }
                            else if (s.NotificationSource.HasFlag(eNotificationSource.NewAnnotation))
                            {
                                await Enqueue(eNodeNotificationType.NewAnnotation, evt, s, ct);
                            }
                        }
                        else if (evt is ServiceRequestPublishedEvent)
                        {
                            if (s.NotificationSource.HasFlag(eNotificationSource.NewCase))
                            {
                                await Enqueue(eNodeNotificationType.NodePublished, evt, s, ct);
                            }
                        }
                    }
                }
            }
        }
    }


    protected bool IsValidBodySite(ServiceRequest sr, ConceptFilter? filter)
    {
        if (filter is not null && filter.ConceptCodes.Any())
        {
            if (!coding.InConceptFilter(sr?.Description?.BodySite?.Code, filter))
            {
                return false;
            }
        }
        return true;
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
            var entity = Notification.Create(t, target, false, ReceiverId, evt.ServiceRequest.Id);
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
