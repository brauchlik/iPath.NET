export function connect(dotNetHelper, url) {
    const es = new EventSource(url, { withCredentials: true });

    es.addEventListener('notification', e => {
        dotNetHelper.invokeMethodAsync('OnNotification', e.data, e.lastEventId);
    });

    es.addEventListener('domain-event', e => {
        dotNetHelper.invokeMethodAsync('OnDomainEvent', e.data, e.lastEventId);
    });

    es.addEventListener('system-event', e => {
        dotNetHelper.invokeMethodAsync('OnSystemEvent', e.data, e.lastEventId);
    });

    es.onerror = () => {
        dotNetHelper.invokeMethodAsync('OnError');
    };

    return { close: () => es.close() };
}
