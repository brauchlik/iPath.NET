using DispatchR.Abstractions.Notification;

namespace iPath.API.Services.Storage;

public class RemoteStorageNotificationHandler(IRemoteStorageService srv) 
    : INotificationHandler<EventEntity>
{
    public async ValueTask Handle(EventEntity evt, CancellationToken cancellationToken)
    {
        // TODO: renaming tasks should be delegated to backgroup worker queue


        if (evt is CommunityRenamedEvent c)
        {
            await srv.RenameCommunity(c.Community);
        }
        else if(evt is GroupRenamedEvent g)
        {
            await srv.RenameGroup(g.Group);
        }
        else if (evt is ServiceRequestDescriptionUpdatedEvent sr)
        {
            await srv.RenameRequest(sr.ServiceRequest);
        }
    }
}
