using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Notifications;

namespace iPath.API.Services.CaseRoom;

public sealed class InMemoryCaseRoomSyncReceiver(INotificationEventBus bus) : ICaseRoomSyncReceiver
{
    public IDisposable Subscribe(Guid requestId, Action<CaseRoomSyncEvent> handler)
        => bus.SubscribeCaseRoomSync(requestId, handler);
}
