using DispatchR.Abstractions.Send;

namespace iPath.Application.Features.Notifications;

public record MarkNotificationAsReadCommand(Guid NotificationId)
    : IRequest<MarkNotificationAsReadCommand, Task<bool>>;
