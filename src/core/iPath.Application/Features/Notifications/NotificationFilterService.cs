using iPath.Application.Coding;
using iPath.Domain.Entities;
using iPath.Domain.Notifications;

namespace iPath.Application.Features.Notifications;

public class NotificationFilterService : INotificationFilterService
{
    private readonly CodingService _coding;

    public NotificationFilterService(CodingService coding)
    {
        _coding = coding;
    }

    public NotificationFilterResult ShouldNotify(
        GroupMember subscription,
        ServiceRequestEvent evt,
        Guid? serviceRequestOwnerId)
    {
        if (evt is not IEventWithNotifications)
        {
            return new NotificationFilterResult(false, "Event does not trigger notifications");
        }

        if (evt.UserId == subscription.UserId)
        {
            return new NotificationFilterResult(false, "User is the event creator");
        }

        var bodySiteFilter = GetBodySiteFilter(subscription);
        var (isValid, skipReason) = ValidateBodySite(evt.ServiceRequest, bodySiteFilter);
        if (!isValid)
        {
            return new NotificationFilterResult(false, skipReason);
        }

        return evt switch
        {
            AnnotationAddedEvent => EvaluateAnnotationEvent(subscription, serviceRequestOwnerId),
            ServiceRequestPublishedEvent => EvaluatePublishedEvent(subscription),
            _ => new NotificationFilterResult(false, $"Unknown event type: {evt.EventName}")
        };
    }

    public (bool isValid, string? skipReason) ValidateBodySite(
        ServiceRequest? serviceRequest,
        ConceptFilter? filter)
    {
        if (filter is null || filter.Concetps.Count == 0)
        {
            return (true, null);
        }

        var bodySiteCode = serviceRequest?.Description?.BodySite?.Code;
        if (string.IsNullOrEmpty(bodySiteCode))
        {
            return (false, "ServiceRequest has no BodySite code");
        }

        if (!_coding.InConceptFilter(bodySiteCode, filter))
        {
            return (false, $"BodySite '{bodySiteCode}' not in filter: {filter.ConceptCodesString}");
        }

        return (true, null);
    }

    private ConceptFilter? GetBodySiteFilter(GroupMember subscription)
    {
        if (subscription.NotificationSettings?.UseProfileBodySiteFilter == true)
        {
            return subscription.User?.Profile?.SpecialisationBodySite;
        }

        return subscription.NotificationSettings?.BodySiteFilter;
    }

    private NotificationFilterResult EvaluateAnnotationEvent(
        GroupMember subscription,
        Guid? serviceRequestOwnerId)
    {
        if (subscription.NotificationSource.HasFlag(eNotificationSource.NewAnnotationOnMyCase))
        {
            if (serviceRequestOwnerId == subscription.UserId)
            {
                return new NotificationFilterResult(
                    true,
                    null,
                    eNodeNotificationType.NewAnnotation);
            }
        }

        if (subscription.NotificationSource.HasFlag(eNotificationSource.NewAnnotation))
        {
            return new NotificationFilterResult(
                true,
                null,
                eNodeNotificationType.NewAnnotation);
        }

        return new NotificationFilterResult(
            false,
            $"User has no NewAnnotation or NewAnnotationOnMyCase subscription flags");
    }

    private NotificationFilterResult EvaluatePublishedEvent(GroupMember subscription)
    {
        if (subscription.NotificationSource.HasFlag(eNotificationSource.NewCase))
        {
            return new NotificationFilterResult(
                true,
                null,
                eNodeNotificationType.NodePublished);
        }

        return new NotificationFilterResult(
            false,
            "User has no NewCase subscription flag");
    }
}