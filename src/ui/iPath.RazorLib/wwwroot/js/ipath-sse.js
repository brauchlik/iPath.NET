export function connect(dotNetHelper, url, lastEventId) {
    const fullUrl = lastEventId ? `${url}?lastEventId=${encodeURIComponent(lastEventId)}` : url;
    const es = new EventSource(fullUrl, { withCredentials: true });

    es.addEventListener('notification', (e) => {
        dotNetHelper.invokeMethodAsync('OnNotification', e.data, e.lastEventId);
    });

    es.addEventListener('domain-event', (e) => {
        dotNetHelper.invokeMethodAsync('OnDomainEvent', e.data, e.lastEventId);
    });

    es.addEventListener('system-event', (e) => {
        dotNetHelper.invokeMethodAsync('OnSystemEvent', e.data, e.lastEventId);
    });

    es.onerror = (e) => {
        dotNetHelper.invokeMethodAsync('OnError');
    };

    return {
        close: () => es.close()
    };
}
