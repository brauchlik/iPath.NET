using DispatchR.Abstractions.Notification;

namespace iPath.EF.Core.FeatureHandlers.Groups;

public class GroupCacheClearedHandler(IGroupCache cache)
    : INotificationHandler<GroupCacheClearedEvent>
{
    public async ValueTask Handle(GroupCacheClearedEvent evt, CancellationToken ct)
    {
        await cache.ClearGroup(evt.GroupId);
    }
}
