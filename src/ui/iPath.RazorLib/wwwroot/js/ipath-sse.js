export function connect(dotNetHelper, url) {
    const es = new EventSource(url, { withCredentials: true });

    es.addEventListener('open', () => {
        console.info('[SSE] connected to %s', url);
    });

    es.addEventListener('notification', e => {
        console.debug('[SSE] notification: id=%s', e.lastEventId);
        dotNetHelper.invokeMethodAsync('OnNotification', e.data, e.lastEventId);
    });

    es.addEventListener('domain-event', e => {
        console.debug('[SSE] domain-event: id=%s data=%s', e.lastEventId, e.data);
        dotNetHelper.invokeMethodAsync('OnDomainEvent', e.data, e.lastEventId);
    });

    es.addEventListener('system-event', e => {
        console.debug('[SSE] system-event: id=%s data=%s', e.lastEventId, e.data);
        dotNetHelper.invokeMethodAsync('OnSystemEvent', e.data, e.lastEventId);
    });

    es.addEventListener('caseroom-sync', e => {
        console.debug('[SSE] caseroom-sync: id=%s data=%s', e.lastEventId, e.data);
        dotNetHelper.invokeMethodAsync('OnCaseRoomSync', e.data, e.lastEventId);
    });

    es.onerror = () => {
        console.warn('[SSE] connection error; reconnecting...');
        dotNetHelper.invokeMethodAsync('OnError');
    };

    return { close: () => es.close() };
}
