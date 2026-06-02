using iPath.API.Services.Notifications;
using iPath.API.Services.Notifications.Publisher;
using iPath.Application.Features.Notifications;
using iPath.Domain.Entities;
using iPath.Domain.Notifications;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace iPath.Test.xUnit2.Notifications;

public class InAppNotificationPublisherTests
{
    [Fact]
    public async Task PublishAsync_ShouldMarkAsSent_WhenDeliverySucceeds()
    {
        var sse = Substitute.For<ISseConnectionManager>();
        var eventBus = Substitute.For<INotificationEventBus>();
        var logger = new LoggerFactory().CreateLogger<InAppNotificationPublisher>();
        var pub = new InAppNotificationPublisher(sse, eventBus, logger);

        var user = new User { Id = Guid.NewGuid(), UserName = "test", Email = "t@test.com", EmailConfirmed = true };
        var n = Notification.Create(eNodeNotificationType.NewAnnotation, eNotificationTarget.InApp, false, user.Id, Guid.NewGuid(), Guid.NewGuid());
        n.GetType().GetProperty("User")!.SetValue(n, user);

        await pub.PublishAsync(n, default);

        await sse.Received().SendToUserAsync(user.Id, "notification", Arg.Any<NotificationDto>(), Arg.Any<string>());
        n.Status.Should().Be(NotificationStatus.Sent);
    }
}
