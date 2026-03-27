using DispatchR.Abstractions.Send;

namespace iPath.Application.Features.Admin;

public record GetEventNotificationFlagsQuery(string EventIds)
    : IRequest<GetEventNotificationFlagsQuery, Dictionary<Guid, int>>;