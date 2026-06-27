using System.Collections.Concurrent;
using iPath.Application.Features.CaseRoom;

namespace iPath.Application.Features.Notifications;

public interface INotificationEventBus
{
    void PublishNotification(Guid userId, NotificationDto dto);
    IDisposable SubscribeNotifications(Guid userId, Action<NotificationDto> handler);

    void PublishDomainEvent(DomainEventSummary evt);
    IDisposable SubscribeDomainEvents(Action<DomainEventSummary> handler);

    void PublishSystemEvent(SystemEventHint hint);
    IDisposable SubscribeSystemEvents(Action<SystemEventHint> handler);

    void PublishCaseRoomSync(CaseRoomSyncEvent evt);
    IDisposable SubscribeCaseRoomSync(Action<CaseRoomSyncEvent> handler);
}

public class NotificationEventBus : INotificationEventBus
{
    private readonly ConcurrentDictionary<Guid, List<Action<NotificationDto>>> _notificationSubs = new();
    private readonly ConcurrentDictionary<Guid, Action<DomainEventSummary>> _domainSubs = new();
    private readonly ConcurrentDictionary<Guid, Action<SystemEventHint>> _systemSubs = new();
    private readonly ConcurrentDictionary<Guid, Action<CaseRoomSyncEvent>> _caseRoomSubs = new();

    public void PublishNotification(Guid userId, NotificationDto dto)
    {
        if (_notificationSubs.TryGetValue(userId, out var handlers))
        {
            foreach (var h in handlers.ToArray())
                h(dto);
        }
    }

    public IDisposable SubscribeNotifications(Guid userId, Action<NotificationDto> handler)
    {
        _notificationSubs.AddOrUpdate(userId,
            _ => [handler],
            (_, list) => { list.Add(handler); return list; });
        return new Unsubscriber(() =>
        {
            if (_notificationSubs.TryGetValue(userId, out var list))
            {
                list.Remove(handler);
                if (list.Count == 0)
                    _notificationSubs.TryRemove(userId, out _);
            }
        });
    }

    public void PublishDomainEvent(DomainEventSummary evt)
    {
        foreach (var h in _domainSubs.Values.ToArray())
            h(evt);
    }

    public IDisposable SubscribeDomainEvents(Action<DomainEventSummary> handler)
    {
        var key = Guid.NewGuid();
        _domainSubs[key] = handler;
        return new Unsubscriber(() => _domainSubs.TryRemove(key, out _));
    }

    public void PublishSystemEvent(SystemEventHint hint)
    {
        foreach (var h in _systemSubs.Values.ToArray())
            h(hint);
    }

    public IDisposable SubscribeSystemEvents(Action<SystemEventHint> handler)
    {
        var key = Guid.NewGuid();
        _systemSubs[key] = handler;
        return new Unsubscriber(() => _systemSubs.TryRemove(key, out _));
    }

    public void PublishCaseRoomSync(CaseRoomSyncEvent evt)
    {
        foreach (var h in _caseRoomSubs.Values.ToArray())
            h(evt);
    }

    public IDisposable SubscribeCaseRoomSync(Action<CaseRoomSyncEvent> handler)
    {
        var key = Guid.NewGuid();
        _caseRoomSubs[key] = handler;
        return new Unsubscriber(() => _caseRoomSubs.TryRemove(key, out _));
    }
}

file class Unsubscriber(Action dispose) : IDisposable
{
    public void Dispose() => dispose();
}
