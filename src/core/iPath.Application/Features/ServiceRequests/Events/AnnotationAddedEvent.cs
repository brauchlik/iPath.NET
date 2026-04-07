namespace iPath.Application.Features.ServiceRequests;

public class AnnotationAddedEvent : ServiceRequestEvent, IEventWithNotifications
{
    public ServiceRequestEvent Event => this;
}
