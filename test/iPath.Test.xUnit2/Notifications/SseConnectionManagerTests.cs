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

        var connectionTask = mgr.AddConnectionAsync(SseChannel.User(userId), ctx.Response, cts.Token);
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

        var taskA = mgr.AddConnectionAsync(SseChannel.User(userA), ctxA.Response, ctsA.Token);
        var taskB = mgr.AddConnectionAsync(SseChannel.User(userB), ctxB.Response, ctsB.Token);

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

    [Fact]
    public async Task Guests_InDifferentRooms_DoNotShareAFanoutBucket()
    {
        // Every CaseRoom guest carries the same synthetic principal (Guid.Empty), so keying
        // connections by user id put all of them in one bucket and delivered room A's
        // caseroom-sync to a guest sitting in room B. They are keyed by (room, session) now.
        var services = new ServiceCollection().BuildServiceProvider();
        var logger = new LoggerFactory().CreateLogger<SseConnectionManager>();
        var mgr = new SseConnectionManager(services, logger);

        var roomA = Guid.NewGuid();
        var roomB = Guid.NewGuid();
        var ctxA = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var ctxB = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var ctsA = new CancellationTokenSource();
        var ctsB = new CancellationTokenSource();

        var taskA = mgr.AddConnectionAsync(SseChannel.Guest(roomA, Guid.NewGuid()), ctxA.Response, ctsA.Token);
        var taskB = mgr.AddConnectionAsync(SseChannel.Guest(roomB, Guid.NewGuid()), ctxB.Response, ctsB.Token);

        await mgr.SendToChannelAsync(SseChannel.Guest(roomA, Guid.Empty), "caseroom-sync", new { msg = "wrong-session" });
        await Task.Delay(200);

        ctsA.Cancel();
        ctsB.Cancel();
        try { await taskA; } catch (OperationCanceledException) { }
        try { await taskB; } catch (OperationCanceledException) { }

        ctxA.Response.Body.Position = 0;
        ctxB.Response.Body.Position = 0;
        (await new StreamReader(ctxA.Response.Body).ReadToEndAsync()).Should().BeEmpty();
        (await new StreamReader(ctxB.Response.Body).ReadToEndAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task GuestChannel_DeliversOnlyToThatRoomAndSession()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var logger = new LoggerFactory().CreateLogger<SseConnectionManager>();
        var mgr = new SseConnectionManager(services, logger);

        var room = Guid.NewGuid();
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();
        var ctxA = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var ctxB = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var ctsA = new CancellationTokenSource();
        var ctsB = new CancellationTokenSource();

        var taskA = mgr.AddConnectionAsync(SseChannel.Guest(room, sessionA), ctxA.Response, ctsA.Token);
        var taskB = mgr.AddConnectionAsync(SseChannel.Guest(room, sessionB), ctxB.Response, ctsB.Token);

        await mgr.SendToChannelAsync(SseChannel.Guest(room, sessionA), "caseroom-sync", new { msg = "hello" });
        await Task.Delay(200);

        ctsA.Cancel();
        ctsB.Cancel();
        try { await taskA; } catch (OperationCanceledException) { }
        try { await taskB; } catch (OperationCanceledException) { }

        ctxA.Response.Body.Position = 0;
        ctxB.Response.Body.Position = 0;
        (await new StreamReader(ctxA.Response.Body).ReadToEndAsync()).Should().Contain("data: {\"msg\":\"hello\"}");
        (await new StreamReader(ctxB.Response.Body).ReadToEndAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task GuestChannel_IsNotReachableByUserChannel()
    {
        // The old shared bucket was literally SendToUserAsync(Guid.Empty, ...). Assert that
        // address can no longer reach a guest.
        var services = new ServiceCollection().BuildServiceProvider();
        var logger = new LoggerFactory().CreateLogger<SseConnectionManager>();
        var mgr = new SseConnectionManager(services, logger);

        var room = Guid.NewGuid();
        var ctx = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var cts = new CancellationTokenSource();

        var task = mgr.AddConnectionAsync(SseChannel.Guest(room, Guid.NewGuid()), ctx.Response, cts.Token);
        await mgr.SendToUserAsync(Guid.Empty, "caseroom-sync", new { msg = "legacy-broadcast" });
        await Task.Delay(200);

        cts.Cancel();
        try { await task; } catch (OperationCanceledException) { }

        ctx.Response.Body.Position = 0;
        (await new StreamReader(ctx.Response.Body).ReadToEndAsync()).Should().BeEmpty();
    }
}
