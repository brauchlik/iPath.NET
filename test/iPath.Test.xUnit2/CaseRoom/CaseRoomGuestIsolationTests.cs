using iPath.API.Services.CaseRoom;
using iPath.API.Services.Notifications;
using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Notifications;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace iPath.Test.xUnit2.CaseRoom;

/// <summary>
/// The two-browsers-in-different-rooms scenario, driven in-process against a real
/// SseConnectionManager and a real CaseRoomSessionStore. This is the level the leak lived at:
/// guests all carry the synthetic Guid.Empty principal, so before channel keying every guest
/// shared one fan-out bucket and received every room's caseroom-sync. The CaseRoomPage filtered
/// them by requestId on arrival, which is why it was invisible in a browser — the data had
/// already crossed the wire.
/// </summary>
public class CaseRoomGuestIsolationTests
{
    private static (DefaultHttpContext ctx, CancellationTokenSource cts) Listener()
        => (new DefaultHttpContext { Response = { Body = new MemoryStream() } }, new CancellationTokenSource());

    private static async Task<string> DrainAsync(DefaultHttpContext ctx, CancellationTokenSource cts, Task connection)
    {
        cts.Cancel();
        try { await connection; } catch (OperationCanceledException) { }
        ctx.Response.Body.Position = 0;
        return await new StreamReader(ctx.Response.Body).ReadToEndAsync();
    }

    private static CaseRoomSessionStore NewStore(SseConnectionManager sse)
        => new(sse, new NotificationEventBus(), new LoggerFactory().CreateLogger<CaseRoomSessionStore>());

    private static SseConnectionManager NewSse()
        => new(new ServiceCollection().BuildServiceProvider(),
               new LoggerFactory().CreateLogger<SseConnectionManager>());

    [Fact]
    public async Task ViewportSync_InOneRoom_DoesNotReachAGuestInAnotherRoom()
    {
        var sse = NewSse();
        using var store = NewStore(sse);

        var roomA = Guid.NewGuid();
        var roomB = Guid.NewGuid();
        var hostA = Guid.NewGuid();
        var hostB = Guid.NewGuid();
        var hostSessionA = Guid.NewGuid();
        var hostSessionB = Guid.NewGuid();
        var guestSessionA = Guid.NewGuid();
        var guestSessionB = Guid.NewGuid();

        var (ctxA, ctsA) = Listener();
        var (ctxB, ctsB) = Listener();
        var streamA = sse.AddConnectionAsync(SseChannel.Guest(roomA, guestSessionA), ctxA.Response, ctsA.Token);
        var streamB = sse.AddConnectionAsync(SseChannel.Guest(roomB, guestSessionB), ctxB.Response, ctsB.Token);

        // Host joins first so it holds control; the guest then joins the same room.
        await store.JoinAsync(roomA, hostSessionA, hostA, "Host A");
        await store.JoinAsync(roomA, guestSessionA, Guid.Empty, "Guest", isGuest: true);
        await store.JoinAsync(roomB, hostSessionB, hostB, "Host B");
        await store.JoinAsync(roomB, guestSessionB, Guid.Empty, "Guest", isGuest: true);

        await store.SyncAsync(roomA, hostSessionA, hostA,
            new SyncPayload(null, new ViewportState(0.4242, 0.1337, 2.5), SessionId: hostSessionA),
            CancellationToken.None);

        await Task.Delay(200);

        var textA = await DrainAsync(ctxA, ctsA, streamA);
        var textB = await DrainAsync(ctxB, ctsB, streamB);

        // The guest in room A sees the viewport.
        textA.Should().Contain("0.4242");

        // The guest in room B sees nothing from room A — neither the viewport nor the room id.
        textB.Should().NotContain("0.4242");
        textB.Should().NotContain(roomA.ToString());
    }

    [Fact]
    public async Task TwoGuests_InTheSameRoom_BothReceiveTheSync()
    {
        // The companion to the isolation test: narrowing the fan-out must not have narrowed it
        // so far that legitimate co-viewers stop receiving events.
        var sse = NewSse();
        using var store = NewStore(sse);

        var room = Guid.NewGuid();
        var host = Guid.NewGuid();
        var hostSession = Guid.NewGuid();
        var guestSession1 = Guid.NewGuid();
        var guestSession2 = Guid.NewGuid();

        var (ctx1, cts1) = Listener();
        var (ctx2, cts2) = Listener();
        var stream1 = sse.AddConnectionAsync(SseChannel.Guest(room, guestSession1), ctx1.Response, cts1.Token);
        var stream2 = sse.AddConnectionAsync(SseChannel.Guest(room, guestSession2), ctx2.Response, cts2.Token);

        await store.JoinAsync(room, hostSession, host, "Host");
        await store.JoinAsync(room, guestSession1, Guid.Empty, "Guest 1", isGuest: true);
        await store.JoinAsync(room, guestSession2, Guid.Empty, "Guest 2", isGuest: true);

        await store.SyncAsync(room, hostSession, host,
            new SyncPayload(null, new ViewportState(0.9191, 0.2, 1.0), SessionId: hostSession),
            CancellationToken.None);

        await Task.Delay(200);

        (await DrainAsync(ctx1, cts1, stream1)).Should().Contain("0.9191");
        (await DrainAsync(ctx2, cts2, stream2)).Should().Contain("0.9191");
    }

    [Fact]
    public async Task HostLeaving_StillReachesTheGuestsItKicks()
    {
        // Guests are removed from the room before the HostLeft event is sent, so their channels
        // have to be captured first. Deleting the old SendToUserAsync(Guid.Empty, ...) broadcast
        // without doing that would have made the kick reach nobody.
        var sse = NewSse();
        using var store = NewStore(sse);

        var room = Guid.NewGuid();
        var host = Guid.NewGuid();
        var hostSession = Guid.NewGuid();
        var guestSession = Guid.NewGuid();

        var (ctx, cts) = Listener();
        var stream = sse.AddConnectionAsync(SseChannel.Guest(room, guestSession), ctx.Response, cts.Token);

        await store.JoinAsync(room, hostSession, host, "Host");
        await store.JoinAsync(room, guestSession, Guid.Empty, "Guest", isGuest: true);

        await store.LeaveAsync(room, hostSession, CancellationToken.None);

        await Task.Delay(200);

        (await DrainAsync(ctx, cts, stream)).Should().Contain("HostLeft");
    }
}
