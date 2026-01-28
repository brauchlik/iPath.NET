using iPath.Domain.Notificxations;

namespace iPath.Application.Features.Notifications;

public record NotificationDto(Guid Id, DateTime Date, eNodeNotificationType EventType, eNotificationTarget Target, OwnerDto Receiver, string? Payload = null);

