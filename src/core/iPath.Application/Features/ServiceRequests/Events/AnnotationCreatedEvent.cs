using iPath.Domain.Notificxations;

namespace iPath.Application.Features.ServiceRequests;

public class AnnotationCreatedEvent : ServiceRequestEvent, IEventWithNotifications
{
    public ServiceRequestEvent Event => this;
}
