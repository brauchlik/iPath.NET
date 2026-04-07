using iPath.Application.Attributes;
using iPath.Domain.Notifications;

namespace iPath.Application.Features.Notifications;

public record NotificationDto(
    Guid Id, 
    [property: SortBy("Date", "CreatedOn")] DateTime Date, 
    eNodeNotificationType EventType, 
    eNotificationTarget Target, 
    [property: SortBy("Receiver.Username", "User.Username")] OwnerDto Receiver, 
    Guid? ServiceRequestId = null,
    Guid? EventId = null,
    string? Payload = null);

