namespace iPath.Application.Features.Admin;

public class GetEventsQuery : PagedQuery<EventDto>
    , IRequest<GetEventsQuery, Task<PagedResultList<EventDto>>>
{
    public string? EventType { get; init; }
    public string? ObjectName { get; init; }
    public Guid? ObjectId { get; init; }
    public bool IncludeNotifications { get; init; } = false;
}


public record EventDto(Guid EventId,
    DateTime EventDate,
    Guid UserId,
    string EventName,
    string ObjectName,
    Guid ObjectId,
    int? NotificationsCount);