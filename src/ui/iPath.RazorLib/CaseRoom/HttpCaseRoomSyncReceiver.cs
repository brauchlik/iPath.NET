using iPath.Application.Features.CaseRoom;
using iPath.Blazor.Componenents.Notifications;

namespace iPath.Blazor.Componenents.CaseRoom;

public sealed class HttpCaseRoomSyncReceiver(SseClientService sse) : ICaseRoomSyncReceiver
{
    public IDisposable Subscribe(Guid requestId, Action<CaseRoomSyncEvent> handler)
    {
        void wrapper(object? s, CaseRoomSyncEvent e)
        {
            if (e.RequestId == requestId) handler(e);
        }
        sse.CaseRoomSyncReceived += wrapper;
        var sub = new SyncUnsubscriber(() =>
        {
            sse.CaseRoomSyncReceived -= wrapper;
        });
        return sub;
    }

    private sealed class SyncUnsubscriber(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
