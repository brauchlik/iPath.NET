using DispatchR.Abstractions.Send;
using iPath.Domain.Entities;

namespace iPath.Application.Features.ServiceRequests;

public record GetServiceRequestEventsQuery(Guid ServiceRequestId)
    : IRequest<GetServiceRequestEventsQuery, Task<List<EventEntity>>>;

public record GetServiceRequestNotificationsQuery(Guid ServiceRequestId)
    : IRequest<GetServiceRequestNotificationsQuery, Task<List<Notifications.NotificationDto>>>;