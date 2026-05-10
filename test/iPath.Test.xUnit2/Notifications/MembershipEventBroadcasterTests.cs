using iPath.API.EventHandlers;
using iPath.API.Services.Notifications;
using iPath.Application.Features.Notifications;
using iPath.Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace iPath.Test.xUnit2.Notifications;

public class MembershipEventBroadcasterTests
{
    [Fact]
    public async Task Handle_ServiceRequestEvent_SendsGroupScopedDomainEvent()
    {
        var sse = Substitute.For<ISseConnectionManager>();
        var logger = new LoggerFactory().CreateLogger<MembershipEventBroadcaster>();
        var handler = new MembershipEventBroadcaster(sse, logger);

        var groupId = Guid.NewGuid();
        var sr = new ServiceRequest { Id = Guid.NewGuid(), GroupId = groupId };
        var evt = new ServiceRequestEvent { ServiceRequest = sr, EventId = Guid.NewGuid(), EventDate = DateTime.UtcNow, EventName = "ServiceRequestEvent" };

        await handler.Handle(evt, default);

        await sse.Received().SendToGroupMembersAsync(
            groupId,
            "domain-event",
            Arg.Is<DomainEventSummary>(s => s.EventType == "ServiceRequestEvent" && s.GroupId == groupId),
            Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_NonServiceRequestEvent_DoesNothing()
    {
        var sse = Substitute.For<ISseConnectionManager>();
        var logger = new LoggerFactory().CreateLogger<MembershipEventBroadcaster>();
        var handler = new MembershipEventBroadcaster(sse, logger);

        var evt = new CommunityCreatedEvent { EventId = Guid.NewGuid(), EventDate = DateTime.UtcNow, EventName = "CommunityCreatedEvent" };

        await handler.Handle(evt, default);

        await sse.DidNotReceive().SendToGroupMembersAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<string>());
    }
}
