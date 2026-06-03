namespace iPath.Application.Features.Notifications;

public record DomainEventSummary(
    string EventType,
    Guid EventId,
    Guid ServiceRequestId,
    Guid GroupId,
    DateTime EventDate);
