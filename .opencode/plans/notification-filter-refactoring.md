# Notification Filter Refactoring Plan

## Executive Summary

Refactor the notification system to improve transparency and testability by:
1. Adding EventId traceability from Notification to Event
2. Extracting filter logic into a testable service
3. Adding comprehensive unit and integration tests
4. Creating API endpoints for querying events/notifications

## Implementation Tasks

### Phase 1: Documentation
- [ ] Create `docs/Notifications-Processing.md`

### Phase 2: Add Event Traceability
- [ ] Modify `src/core/iPath.Domain/Entities/Notifications/Notification.cs` - Add EventId property
- [ ] Modify `src/infrastructure/iPath.API/Services/Notifications/Processors/ServiceRequestEventProcessor.cs` - Pass EventId

### Phase 3: Extract Filter Logic
- [ ] Create `src/core/iPath.Application/Features/Notifications/INotificationFilterService.cs`
- [ ] Create `src/core/iPath.Application/Features/Notifications/NotificationFilterService.cs`
- [ ] Refactor processor to use filter service

### Phase 4: Add API Endpoints
- [ ] Create `src/infrastructure/iPath.API/Endpoints/ServiceRequestEventEndpoints.cs`
- [ ] Register endpoints in MapEndpoints

### Phase 5: Unit Tests
- [ ] Create `test/iPath.Test.xUnit2/Notifications/NotificationFilterTests.cs`

### Phase 6: Integration Tests
- [ ] Create `test/iPath.Test.xUnit2/Notifications/NotificationIntegrationTests.cs`

## Files to Modify

| File | Action |
|------|--------|
| `src/core/iPath.Domain/Entities/Notifications/Notification.cs` | Add EventId property |
| `src/infrastructure/iPath.API/Services/Notifications/Processors/ServiceRequestEventProcessor.cs` | Use filter service, pass EventId |

## Files to Create

| File | Description |
|------|-------------|
| `docs/Notifications-Processing.md` | Documentation with mermaid diagrams |
| `src/core/iPath.Application/Features/Notifications/INotificationFilterService.cs` | Filter service interface |
| `src/core/iPath.Application/Features/Notifications/NotificationFilterService.cs` | Filter service implementation |
| `src/infrastructure/iPath.API/Endpoints/ServiceRequestEventEndpoints.cs` | Query endpoints |
| `test/iPath.Test.xUnit2/Notifications/NotificationFilterTests.cs` | Unit tests |
| `test/iPath.Test.xUnit2/Notifications/NotificationIntegrationTests.cs` | Integration tests |

## Key Code Changes

### Notification.cs - Add EventId

```csharp
public Guid? EventId { get; private set; }

public static Notification Create(
    eNodeNotificationType type, 
    eNotificationTarget target, 
    bool dailySummary, 
    Guid userId, 
    Guid serviceRequestId,
    Guid eventId)  // NEW parameter
{
    return new Notification
    {
        Id = Guid.CreateVersion7(),
        CreatedOn = DateTime.UtcNow,
        UserId = userId,
        EventType = type,
        Target = target,
        DailySummary = dailySummary,
        Status = NotificationStatus.Pending,
        ServiceRequestId = serviceRequestId,
        EventId = eventId  // NEW
    };
}
```

### INotificationFilterService.cs - Interface

```csharp
public interface INotificationFilterService
{
    bool ShouldNotify(GroupMember subscription, ServiceRequestEvent evt, Guid? ownerId);
    (bool isValid, string? reason) ValidateBodySite(ServiceRequest sr, ConceptFilter? filter);
}
```

### ServiceRequestEventProcessor.cs - Refactored

```csharp
public class ServiceRequestEventProcessor(
    iPathDbContext db,
    ILogger<ServiceRequestEventProcessor> logger,
    INotificationQueue queue,
    INotificationFilterService filter)  // Injected
    : IServiceRequestEventProcessor
{
    public async Task ProcessEvent(ServiceRequestEvent evt, CancellationToken ct)
    {
        if (evt is IEventWithNotifications)
        {
            var subscriptions = await db.Set<GroupMember>()
                .Include(m => m.User)
                .AsNoTracking()
                .Where(m => m.User.IsActive)
                .Where(m => m.GroupId == evt.ServiceRequest.GroupId && m.NotificationSource != eNotificationSource.None)
                .ToListAsync(ct);

            foreach (var s in subscriptions)
            {
                if (evt.UserId == s.UserId) continue;

                var (shouldNotify, reason) = filter.ShouldNotify(s, evt, evt.ServiceRequest.OwnerId);
                if (!shouldNotify)
                {
                    logger.LogInformation("Notification skipped: {Reason}", reason);
                    continue;
                }

                await Enqueue(evt, s, ct);
            }
        }
    }
}
```

## Testing Strategy

### Unit Tests (NotificationFilterTests.cs)

1. `IsValidBodySite_NoFilter_ReturnsTrue()`
2. `IsValidBodySite_MatchingCode_ReturnsTrue()`
3. `IsValidBodySite_NonMatchingCode_ReturnsFalse()`
4. `IsValidBodySite_ChildCodeInHierarchy_ReturnsTrue()`
5. `ShouldNotify_SameUser_ReturnsFalse()`
6. `ShouldNotify_NewCaseFlag_ReturnsTrue()`
7. `ShouldNotify_NewAnnotationFlag_ReturnsTrue()`
8. `ShouldNotify_NewAnnotationOnMyCase_OwnerMatch_ReturnsTrue()`
9. `ShouldNotify_NewAnnotationOnMyCase_NonOwner_ReturnsFalse()`
10. `ShouldNotify_UsesProfileFilter_WhenConfigured()`

### Integration Tests (NotificationIntegrationTests.cs)

1. `CreateAnnotation_StoresEventInEventStore()`
2. `CreateAnnotation_CreatesNotificationForSubscribedUser()`
3. `CreateAnnotation_SkipsNotificationForFilteredBodySite()`
4. `CreateAnnotation_DoesNotNotifyCreator()`
5. `PublishServiceRequest_CreatesNewCaseNotification()`

## Dependencies

- Existing: `CodingService` for ICD-O hierarchy lookups
- Existing: `iPathDbContext` for database access
- New: `INotificationFilterService` registration in DI

## Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Breaking existing notifications | EventId is nullable, migrate existing data |
| Filter logic regression | Comprehensive unit tests first |
| Performance impact | Filter service is stateless, no additional DB queries |

## Rollout

1. Add EventId to Notification (non-breaking, nullable)
2. Create filter service with tests
3. Refactor processor to use filter service
4. Add API endpoints
5. Add integration tests
6. Update documentation