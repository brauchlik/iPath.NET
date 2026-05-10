using iPath.API.Services.Notifications;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace iPath.Test.xUnit2.Notifications;

public class SseConnectionManagerTests
{
    [Fact]
    public async Task SendToUserAsync_DeliversMessageToConnectedUser()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var logger = new LoggerFactory().CreateLogger<SseConnectionManager>();
        var mgr = new SseConnectionManager(services, logger);

        var userId = Guid.NewGuid();
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        var cts = new CancellationTokenSource();

        var connectionTask = mgr.AddConnectionAsync(userId, ctx.Response, cts.Token);
        await mgr.SendToUserAsync(userId, "test-event", new { foo = "bar" });
        await Task.Delay(200);
        cts.Cancel();
        try { await connectionTask; } catch (OperationCanceledException) { }

        ctx.Response.Body.Position = 0;
        var text = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        text.Should().Contain("event: test-event");
        text.Should().Contain("data: {\"foo\":\"bar\"}");
    }

    [Fact]
    public async Task SendToUserAsync_DoesNotDeliverToOtherUser()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var logger = new LoggerFactory().CreateLogger<SseConnectionManager>();
        var mgr = new SseConnectionManager(services, logger);

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var ctxA = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var ctxB = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var ctsA = new CancellationTokenSource();
        var ctsB = new CancellationTokenSource();

        var taskA = mgr.AddConnectionAsync(userA, ctxA.Response, ctsA.Token);
        var taskB = mgr.AddConnectionAsync(userB, ctxB.Response, ctsB.Token);

        await mgr.SendToUserAsync(userA, "test", new { msg = "hello" });
        await Task.Delay(200);

        ctsA.Cancel();
        ctsB.Cancel();
        try { await taskA; } catch (OperationCanceledException) { }
        try { await taskB; } catch (OperationCanceledException) { }

        ctxA.Response.Body.Position = 0;
        ctxB.Response.Body.Position = 0;
        var textA = await new StreamReader(ctxA.Response.Body).ReadToEndAsync();
        var textB = await new StreamReader(ctxB.Response.Body).ReadToEndAsync();

        textA.Should().Contain("data: {\"msg\":\"hello\"}");
        textB.Should().BeEmpty();
    }
}
