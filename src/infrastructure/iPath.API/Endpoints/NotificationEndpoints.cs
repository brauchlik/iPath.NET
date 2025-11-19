using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Scalar.AspNetCore;
using System.Runtime.CompilerServices;

namespace iPath.API;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationApi(this IEndpointRouteBuilder route)
    {
        route.MapGet("food", (NotificationService srv, CancellationToken ct) =>
            TypedResults.ServerSentEvents(srv.GetEvent(ct), eventType: "myevent"))
            .WithTags("Notifications");

        return route;
    }
}

public record myevent(Guid Id, DateTime date, string data);


public class NotificationService
{
    public async IAsyncEnumerable<myevent> GetEvent(
        [EnumeratorCancellation] CancellationToken ct)
    {
        int i = 0;
        while (ct is not { IsCancellationRequested: true })
        {
            i++;
            yield return new myevent(Guid.CreateVersion7(), DateTime.UtcNow, i.ToString());

            await Task.Delay(1000, ct);
        }
    }
}
