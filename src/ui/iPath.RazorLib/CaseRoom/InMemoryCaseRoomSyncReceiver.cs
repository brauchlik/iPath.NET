using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Notifications;

namespace iPath.Blazor.Componenents.CaseRoom;

public sealed class InMemoryCaseRoomSyncReceiver(INotificationEventBus bus) : ICaseRoomSyncReceiver
{
    public IDisposable Subscribe(Guid requestId, Action<CaseRoomSyncEvent> handler)
    {
        void filtered(CaseRoomSyncEvent e)
        {
            if (e.RequestId == requestId) handler(e);
        }
        return bus.SubscribeCaseRoomSync(filtered);
    }
}
