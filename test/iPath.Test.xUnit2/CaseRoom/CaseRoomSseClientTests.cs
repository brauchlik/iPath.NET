using iPath.Blazor.Componenents.Notifications;
using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NSubstitute;
using FluentAssertions;

namespace iPath.Test.xUnit2.CaseRoom;

public class CaseRoomSseClientTests
{
    [Fact]
    public void OnCaseRoomSync_RaisesEventWithDeserializedPayload()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var logger = new LoggerFactory().CreateLogger<SseClientService>();
        var js = Substitute.For<IJSRuntime>();
        var service = new SseClientService(js, services, logger);

        var received = new List<CaseRoomSyncEvent>();
        service.CaseRoomSyncReceived += (_, e) => received.Add(e);

        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var payload = $"{{\"requestId\":\"{requestId}\",\"userId\":\"{userId}\",\"displayName\":\"Alice\",\"payload\":{{\"documentId\":null,\"viewport\":{{\"x\":0.5,\"y\":0.5,\"zoom\":2.0}}}},\"timestamp\":\"2026-06-27T12:00:00+00:00\"}}";

        var lastEventId = DateTimeOffset.UtcNow.ToString("o");
        service.OnCaseRoomSync(payload, lastEventId);

        received.Should().ContainSingle();
        received[0].RequestId.Should().Be(requestId);
        received[0].DisplayName.Should().Be("Alice");
        received[0].Payload.Viewport!.Zoom.Should().Be(2.0);
    }
}
