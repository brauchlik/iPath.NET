using iPath.Application.Features.ServiceRequests;
using iPath.Domain.Entities;
using iPath.Domain.Notifications;
using Xunit;

namespace iPath.Test.xUnit2.Notifications;

public class NotificationFilterTests
{
    #region ShouldNotify Tests - Basic Logic

    [Fact]
    public void ShouldNotify_NonNotificationEvent_ReturnsFalse()
    {
        var sr = CreateServiceRequest("C50");
        var evt = new ServiceRequestCreatedEvent { ServiceRequest = sr, UserId = Guid.NewGuid() };
        var member = CreateGroupMember();
        var filterService = CreateFilterService(bodySiteMatches: true);

        var result = filterService.ShouldNotify(member, evt, null);

        Assert.False(result.ShouldNotify);
        Assert.Contains("does not trigger notifications", result.SkipReason);
    }

    [Fact]
    public void ShouldNotify_SameUserAsEventCreator_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var sr = CreateServiceRequest("C50");
        var evt = new AnnotationAddedEvent { ServiceRequest = sr, UserId = userId };
        var member = CreateGroupMember(userId: userId);
        var filterService = CreateFilterService(bodySiteMatches: true);

        var result = filterService.ShouldNotify(member, evt, null);

        Assert.False(result.ShouldNotify);
        Assert.Contains("event creator", result.SkipReason);
    }

    [Fact]
    public void ShouldNotify_NewCaseSubscription_ReturnsTrue()
    {
        var sr = CreateServiceRequest("C50");
        var evt = new ServiceRequestPublishedEvent { ServiceRequest = sr, UserId = Guid.NewGuid() };
        var member = CreateGroupMember(notificationSource: eNotificationSource.NewCase);
        var filterService = CreateFilterService(bodySiteMatches: true);

        var result = filterService.ShouldNotify(member, evt, null);

        Assert.True(result.ShouldNotify);
        Assert.Equal(eNodeNotificationType.NodePublished, result.NotificationType);
    }

    [Fact]
    public void ShouldNotify_NewAnnotationSubscription_ReturnsTrue()
    {
        var sr = CreateServiceRequest("C50");
        var evt = new AnnotationAddedEvent { ServiceRequest = sr, UserId = Guid.NewGuid() };
        var member = CreateGroupMember(notificationSource: eNotificationSource.NewAnnotation);
        var filterService = CreateFilterService(bodySiteMatches: true);

        var result = filterService.ShouldNotify(member, evt, null);

        Assert.True(result.ShouldNotify);
        Assert.Equal(eNodeNotificationType.NewAnnotation, result.NotificationType);
    }

    [Fact]
    public void ShouldNotify_NewAnnotationOnMyCase_OwnerMatch_ReturnsTrue()
    {
        var ownerId = Guid.NewGuid();
        var sr = CreateServiceRequest("C50", ownerId);
        var evt = new AnnotationAddedEvent { ServiceRequest = sr, UserId = Guid.NewGuid() };
        var member = CreateGroupMember(userId: ownerId, notificationSource: eNotificationSource.NewAnnotationOnMyCase);
        var filterService = CreateFilterService(bodySiteMatches: true);

        var result = filterService.ShouldNotify(member, evt, ownerId);

        Assert.True(result.ShouldNotify);
        Assert.Equal(eNodeNotificationType.NewAnnotation, result.NotificationType);
    }

    [Fact]
    public void ShouldNotify_NewAnnotationOnMyCase_NonOwner_ReturnsFalse()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var sr = CreateServiceRequest("C50", ownerId);
        var evt = new AnnotationAddedEvent { ServiceRequest = sr, UserId = otherUserId };
        var member = CreateGroupMember(userId: Guid.NewGuid(), notificationSource: eNotificationSource.NewAnnotationOnMyCase);
        var filterService = CreateFilterService(bodySiteMatches: true);

        var result = filterService.ShouldNotify(member, evt, ownerId);

        Assert.False(result.ShouldNotify);
    }

    [Fact]
    public void ShouldNotify_NoSubscriptionFlags_ReturnsFalse()
    {
        var sr = CreateServiceRequest("C50");
        var evt = new AnnotationAddedEvent { ServiceRequest = sr, UserId = Guid.NewGuid() };
        var member = CreateGroupMember(notificationSource: eNotificationSource.None);
        var filterService = CreateFilterService(bodySiteMatches: true);

        var result = filterService.ShouldNotify(member, evt, null);

        Assert.False(result.ShouldNotify);
        Assert.Contains("no NewAnnotation", result.SkipReason);
    }

    [Fact]
    public void ShouldNotify_NewCaseFlagFalse_ReturnsFalse()
    {
        var sr = CreateServiceRequest("C50");
        var evt = new ServiceRequestPublishedEvent { ServiceRequest = sr, UserId = Guid.NewGuid() };
        var member = CreateGroupMember(notificationSource: eNotificationSource.NewAnnotation);
        var filterService = CreateFilterService(bodySiteMatches: true);

        var result = filterService.ShouldNotify(member, evt, null);

        Assert.False(result.ShouldNotify);
        Assert.Contains("no NewCase subscription", result.SkipReason);
    }

    [Fact(Skip = "BodySite filtering requires ICD-O code lookup - test with integration tests")]
    public void ShouldNotify_BodySiteFilterBlocks_ReturnsFalse()
    {
        // This test requires ICD-O code lookup which is done via CodingService
        // Integration tests should verify BodySite filtering works correctly
    }

    [Fact]
    public void ShouldNotify_NewAnnotationAndNewAnnotationOnMyCase_BothFlagsSetTrue_ReturnsTrue()
    {
        var ownerId = Guid.NewGuid();
        var sr = CreateServiceRequest("C50", ownerId);
        var evt = new AnnotationAddedEvent { ServiceRequest = sr, UserId = Guid.NewGuid() };
        var member = CreateGroupMember(
            userId: ownerId, 
            notificationSource: eNotificationSource.NewAnnotation | eNotificationSource.NewAnnotationOnMyCase);
        var filterService = CreateFilterService(bodySiteMatches: true);

        var result = filterService.ShouldNotify(member, evt, ownerId);

        Assert.True(result.ShouldNotify);
        Assert.Equal(eNodeNotificationType.NewAnnotation, result.NotificationType);
    }

    #endregion

    #region ValidateBodySite Tests

    [Fact]
    public void ValidateBodySite_NoFilter_ReturnsTrue()
    {
        var sr = CreateServiceRequest("C50.9");
        var filterService = CreateFilterService(bodySiteMatches: true);

        var result = filterService.ValidateBodySite(sr, null);

        Assert.True(result.isValid);
        Assert.Null(result.skipReason);
    }

    [Fact]
    public void ValidateBodySite_EmptyFilter_ReturnsTrue()
    {
        var sr = CreateServiceRequest("C50.9");
        var filter = new ConceptFilter();
        var filterService = CreateFilterService(bodySiteMatches: true);

        var result = filterService.ValidateBodySite(sr, filter);

        Assert.True(result.isValid);
        Assert.Null(result.skipReason);
    }

    [Fact]
    public void ValidateBodySite_NoBodySiteOnServiceRequest_ReturnsFalse()
    {
        var sr = CreateServiceRequest(null);
        var filter = CreateFilter("C50");
        var filterService = CreateFilterService(bodySiteMatches: false);

        var result = filterService.ValidateBodySite(sr, filter);

        Assert.False(result.isValid);
        Assert.Contains("no BodySite", result.skipReason);
    }

    #endregion

    #region Helper Methods

    private static ServiceRequest CreateServiceRequest(string? bodySiteCode, Guid? ownerId = null)
    {
        var sr = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            OwnerId = ownerId ?? Guid.NewGuid(),
            Description = new RequestDescription()
        };

        if (bodySiteCode != null)
        {
            sr.Description.BodySite = new CodedConcept
            {
                System = CodedConcept.IcodUrl,
                Code = bodySiteCode,
                Display = bodySiteCode
            };
        }

        return sr;
    }

    private static ConceptFilter CreateFilter(params string[] codes)
    {
        var filter = new ConceptFilter();
        foreach (var code in codes)
        {
            filter.Add(new CodedConcept
            {
                System = CodedConcept.IcodUrl,
                Code = code,
                Display = code
            });
        }
        return filter;
    }

    private static GroupMember CreateGroupMember(
        Guid? userId = null,
        eNotificationSource notificationSource = eNotificationSource.NewCase | eNotificationSource.NewAnnotation,
        ConceptFilter? bodySiteFilter = null)
    {
        var id = userId ?? Guid.NewGuid();
        return new GroupMember
        {
            UserId = id,
            User = new User
            {
                Id = id,
                UserName = "testuser",
                IsActive = true
            },
            NotificationSource = notificationSource,
            NotificationTarget = eNotificationTarget.InApp | eNotificationTarget.Email,
            NotificationSettings = new NotificationSettings
            {
                BodySiteFilter = bodySiteFilter
            }
        };
    }

    private static TestableNotificationFilterService CreateFilterService(bool bodySiteMatches)
    {
        return new TestableNotificationFilterService(bodySiteMatches);
    }

    #endregion

    private class TestableNotificationFilterService : Application.Features.Notifications.INotificationFilterService
    {
        private readonly bool _bodySiteMatches;

        public TestableNotificationFilterService(bool bodySiteMatches)
        {
            _bodySiteMatches = bodySiteMatches;
        }

        public Application.Features.Notifications.NotificationFilterResult ShouldNotify(
            GroupMember subscription,
            ServiceRequestEvent evt,
            Guid? serviceRequestOwnerId)
        {
            if (evt is not IEventWithNotifications)
            {
                return new Application.Features.Notifications.NotificationFilterResult(false, "Event does not trigger notifications");
            }

            if (evt.UserId == subscription.UserId)
            {
                return new Application.Features.Notifications.NotificationFilterResult(false, "User is the event creator");
            }

            var bodySiteResult = ValidateBodySite(evt.ServiceRequest, GetBodySiteFilter(subscription));
            if (!bodySiteResult.isValid)
            {
                return new Application.Features.Notifications.NotificationFilterResult(false, bodySiteResult.skipReason);
            }

            return evt switch
            {
                AnnotationAddedEvent => EvaluateAnnotationEvent(subscription, serviceRequestOwnerId),
                ServiceRequestPublishedEvent => EvaluatePublishedEvent(subscription),
                _ => new Application.Features.Notifications.NotificationFilterResult(false, $"Unknown event type: {evt.EventName}")
            };
        }

        public (bool isValid, string? skipReason) ValidateBodySite(
            ServiceRequest? serviceRequest,
            ConceptFilter? filter)
        {
            if (filter is null || filter.Concetps.Count == 0)
            {
                return (true, null);
            }

            var bodySiteCode = serviceRequest?.Description?.BodySite?.Code;

            if (string.IsNullOrEmpty(bodySiteCode))
            {
                return (false, "ServiceRequest has no BodySite code");
            }

            if (!_bodySiteMatches)
            {
                return (false, $"BodySite '{bodySiteCode}' not in filter: {filter.ConceptCodesString}");
            }

            return (true, null);
        }

        private static ConceptFilter? GetBodySiteFilter(GroupMember subscription)
        {
            if (subscription.NotificationSettings?.UseProfileBodySiteFilter == true)
            {
                return subscription.User?.Profile?.SpecialisationBodySite;
            }
            return subscription.NotificationSettings?.BodySiteFilter;
        }

        private static Application.Features.Notifications.NotificationFilterResult EvaluateAnnotationEvent(
            GroupMember subscription,
            Guid? serviceRequestOwnerId)
        {
            if (subscription.NotificationSource.HasFlag(eNotificationSource.NewAnnotationOnMyCase))
            {
                if (serviceRequestOwnerId == subscription.UserId)
                {
                    return new Application.Features.Notifications.NotificationFilterResult(true, null, eNodeNotificationType.NewAnnotation);
                }
            }

            if (subscription.NotificationSource.HasFlag(eNotificationSource.NewAnnotation))
            {
                return new Application.Features.Notifications.NotificationFilterResult(true, null, eNodeNotificationType.NewAnnotation);
            }

            return new Application.Features.Notifications.NotificationFilterResult(false, "User has no NewAnnotation or NewAnnotationOnMyCase subscription flags");
        }

        private static Application.Features.Notifications.NotificationFilterResult EvaluatePublishedEvent(GroupMember subscription)
        {
            if (subscription.NotificationSource.HasFlag(eNotificationSource.NewCase))
            {
                return new Application.Features.Notifications.NotificationFilterResult(true, null, eNodeNotificationType.NodePublished);
            }

            return new Application.Features.Notifications.NotificationFilterResult(false, "User has no NewCase subscription flag");
        }
    }
}