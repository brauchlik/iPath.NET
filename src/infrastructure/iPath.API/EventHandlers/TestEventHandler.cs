using DispatchR.Abstractions.Notification;
using iPath.API.Services.Notifications;

namespace iPath.API.EventHandlers;

public class TestEventHandler(ISseConnectionManager sse, IUserSession sess)
    : INotificationHandler<TestEvent>
{
    public async ValueTask Handle(TestEvent request, CancellationToken cancellationToken)
    {
        if (sess.User is not null)
        {
            await sse.BroadcastAsync("system-event", new { message = request.Message, user = sess.User.Username });
        }
    }
}
