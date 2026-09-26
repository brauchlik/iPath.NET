using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

namespace iPath.API;

public static class TestEndpoints
{
    public static IEndpointRouteBuilder MapTestApi(this IEndpointRouteBuilder route)
    {
        var test = route.MapGroup("test")
                .WithTags("Test");

        test.MapPost("notify", async (TestEvent evt, [FromServices] IMediator mediator, CancellationToken ct)
                => await mediator.Publish(evt, ct))
                .RequireAuthorization();

        return route;
    }
}