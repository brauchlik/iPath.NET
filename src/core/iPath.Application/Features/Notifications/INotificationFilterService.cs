using iPath.Domain.Entities;
using iPath.Domain.Notificxations;

namespace iPath.Application.Features.Notifications;

public interface INotificationFilterService
{
    NotificationFilterResult ShouldNotify(
        GroupMember subscription,
        ServiceRequestEvent evt,
        Guid? serviceRequestOwnerId);

    (bool isValid, string? skipReason) ValidateBodySite(
        ServiceRequest? serviceRequest,
        ConceptFilter? filter);
}

public record NotificationFilterResult(
    bool ShouldNotify,
    string? SkipReason = null,
    eNodeNotificationType? NotificationType = null);