using iPath.Domain.Notifications;

namespace iPath.Application.Features.ServiceRequests;

public class ServiceRequestPublishedEvent : ServiceRequestEvent, IEventWithNotifications;