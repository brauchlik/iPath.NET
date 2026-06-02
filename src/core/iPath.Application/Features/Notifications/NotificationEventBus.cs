namespace iPath.Application.Features.Notifications;

public interface INotificationEventBus
{
    event EventHandler<NotificationDto>? NotificationReceived;
    event EventHandler<DomainEventSummary>? DomainEventReceived;
    event EventHandler<SystemEventHint>? SystemEventReceived;

    void PublishNotification(NotificationDto dto);
    void PublishDomainEvent(DomainEventSummary evt);
    void PublishSystemEvent(SystemEventHint hint);
}

public class NotificationEventBus : INotificationEventBus
{
    public event EventHandler<NotificationDto>? NotificationReceived;
    public event EventHandler<DomainEventSummary>? DomainEventReceived;
    public event EventHandler<SystemEventHint>? SystemEventReceived;

    public void PublishNotification(NotificationDto dto)
        => NotificationReceived?.Invoke(this, dto);

    public void PublishDomainEvent(DomainEventSummary evt)
        => DomainEventReceived?.Invoke(this, evt);

    public void PublishSystemEvent(SystemEventHint hint)
        => SystemEventReceived?.Invoke(this, hint);
}
