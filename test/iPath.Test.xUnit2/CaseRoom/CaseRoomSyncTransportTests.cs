using iPath.API.Services.CaseRoom;
using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Notifications;
using iPath.Blazor.Componenents.CaseRoom;
using iPath.Blazor.Componenents.Notifications;
using iPath.Blazor.ServiceLib.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NSubstitute;
using FluentAssertions;
using iPath.Application.Contracts;

namespace iPath.Test.xUnit2.CaseRoom;

public class CaseRoomSyncTransportTests
{
    [Fact]
    public async Task InMemorySync_PublishesViaEventBusAndReachesReceiver()
    {
        var sseMgr = Substitute.For<iPath.API.Services.Notifications.ISseConnectionManager>();
        var bus = new NotificationEventBus();
        var store = new CaseRoomSessionStore(sseMgr, bus, new LoggerFactory().CreateLogger<CaseRoomSessionStore>());

        var received = new List<CaseRoomSyncEvent>();
        var receiver = new InMemoryCaseRoomSyncReceiver(bus);
        var requestId = Guid.NewGuid();

        var sub = receiver.Subscribe(requestId, e =>
        {
            if (e.RequestId == requestId) received.Add(e);
        });

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await store.JoinAsync(requestId, userA, "Alice", default);
        await store.JoinAsync(requestId, userB, "Bob", default);

        await store.SyncAsync(requestId, userA, new SyncPayload(null, new ViewportState(0.5, 0.5, 2.0)), default);

        received.Should().NotBeEmpty();
        sub.Dispose();
    }

    [Fact]
    public async Task HttpReceiver_ForwardsSseClientServiceEvents()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var logger = new LoggerFactory().CreateLogger<SseClientService>();
        var js = Substitute.For<IJSRuntime>();
        var sseService = new SseClientService(js, services, logger);

        var receiver = new HttpCaseRoomSyncReceiver(sseService);
        var received = new List<CaseRoomSyncEvent>();
        var requestId = Guid.NewGuid();
        var sub = receiver.Subscribe(requestId, e =>
        {
            if (e.RequestId == requestId) received.Add(e);
        });

        var evt = new CaseRoomSyncEvent(requestId, Guid.NewGuid(), "Alice",
            new SyncPayload(null, new ViewportState(1, 1, 1)), DateTimeOffset.UtcNow);

        var json = System.Text.Json.JsonSerializer.Serialize(evt, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        sseService.OnCaseRoomSync(json, DateTimeOffset.UtcNow.ToString("o"));

        received.Should().ContainSingle();
        received[0].DisplayName.Should().Be("Alice");
        sub.Dispose();
    }
}
