namespace iPath.Application.Features.Notifications;

public record SseMessage(string EventType, string Data, string? Id = null);
