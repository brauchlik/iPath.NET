# Task Assignment System — Design Spec

## 1. Motivation

The current system supports discussion groups where case notifications are broadcast
to subscribers based on BodySite filters. For expert groups, there is no mechanism
to explicitly assign a case to a consultant, track progress, or manage follow-ups.

The Task Assignment system adds formal accountability while coexisting with existing
notifications.

## 2. Core Entity

```csharp
public class TaskAssignment : AuditableEntityWithEvents
{
    public Guid ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; }

    public Guid AssignedToUserId { get; set; }
    public User AssignedToUser { get; set; }

    public Guid? AssignedByUserId { get; set; }  // system, moderator, or self
    public User? AssignedByUser { get; set; }

    public eTaskType Type { get; set; }
    public eTaskAssignmentMode Mode { get; set; }
    public eTaskStatus Status { get; set; }

    public string? Notes { get; set; }
    public DateTime? AcceptedOn { get; set; }
    public DateTime? CompletedOn { get; set; }
    public DateTime? Deadline { get; set; }
    public int? AttemptNumber { get; set; }  // for auto-assign fallback chain
}
```

### Enums

```csharp
public enum eTaskType
{
    DiagnosticReview = 0,
    FollowUp = 1
}

public enum eTaskStatus
{
    Proposed = 0,                // waiting for user to accept
    Assigned = 1,                // accepted or directly assigned
    InProgress = 2,              // actively working
    Completed = 3,
    Declined = 4,                // user refused
    ReturnedForReassignment = 5, // user returned to pool/moderator
    Cancelled = 6                // moderator/admin revokes (any state)
}

public enum eTaskAssignmentMode
{
    SelfAssigned = 0,        // user picked it up
    AutoAssigned = 1,        // system selected via algorithm
    ModeratorSuggested = 2,  // moderator proposed
    DirectAssigned = 3       // mandatory (e.g. follow-up, no accept/decline)
}
```

## 3. Group Configuration

Extend `GroupSettings` in `src/core/iPath.Domain/Entities/Groups/GroupSettings.cs`:

```csharp
public enum eTaskAssignmentStrategy
{
    None = 0,           // discussion groups — no tasks, pure notifications
    SelfService = 1,    // first-come-first-served
    AutoAssign = 2,     // system picks with fallback chain
    Moderated = 3,      // moderator assigns manually
    SelfOrModerated = 4 // notify all + moderator can pre-assign
}
```

New fields on `GroupSettings`:
- `eTaskAssignmentStrategy TaskAssignmentStrategy` (default `None`)
- `int? AutoAssignTimeoutHours` (default `24`, used by AutoAssign strategy)

## 4. State Machine

```
                  ┌──────────────────────────┐
                  │        Proposed           │ ←───── Cancelled
                  │   (needs acceptance)      │
                  └─────┬───────┬─────────────┘
                   accept│       │decline
              ┌──────────▼──┐  ┌─▼────────────┐
              │   Assigned   │  │   Declined   │
              │ (or Direct)  │  │              │
              └──────────┬───┘  └──────────────┘
              start work │
              ┌──────────▼──────┐
              │   InProgress    │ ←───── Cancelled (moderator revokes)
              └────┬────────┬───┘
           return  │        │ complete
    ┌──────────────▼──┐  ┌──▼──────────┐
    │ ReturnedFor-    │  │  Completed  │
    │ Reassignment    │  │             │
    └────────┬────────┘  └─────────────┘
             │ (back to Proposed or moderator pool)
```

**Cancelled** is valid from any state — moderator/admin last resort for unforeseen
situations.

**DirectAssigned** flow: `Assigned → InProgress → Completed` (no accept/decline,
can still be Cancelled by moderator).

## 5. Assignment Strategies — Per-Strategy Flow

### None (Discussion Groups)
No change to current behavior. Notifications fire per existing BodySite/Subscription logic.

### SelfService (First-come-first-served)
1. Case published → notification to all group consultants
2. Consultant views case → clicks [Accept Case]
3. Task created: `Status=Assigned, Mode=SelfAssigned`
4. Multiple consultants can be assigned simultaneously

### AutoAssign (System picks)
1. Case published → system selects best candidate consultant
   (by BodySite match, workload balance, round-robin)
2. Task created: `Status=Proposed, Mode=AutoAssigned`
3. Notification sent to selected consultant
4. If accept within timeout → `Assigned`
5. If decline or timeout → increment `AttemptNumber`, select next candidate
6. If all candidates exhausted → notify group moderators

### Moderated (Moderator assigns)
1. Moderator selects consultant manually
2. Task created: `Status=Proposed, Mode=ModeratorSuggested`
3. Consultant notified
4. Accept → `Assigned`; Decline → moderators notified for reassignment

### SelfOrModerated (Hybrid)
Combines SelfService and Moderated — consultants can self-assign, but moderators
can also pre-assign proactively.

## 6. Task Types

### DiagnosticReview
- Created automatically (SelfService/AutoAssign) or by moderator
- Completing a review may correlate with adding a FinalAssessment annotation
- Status reflects the review workflow

### FollowUp
- Created by the assigned consultant, targeted at the case owner (sender)
- `Mode=DirectAssigned`, `Status=Assigned`
- Sender provides follow-up info, marks complete
- Not a proposal — mandatory for the recipient

## 7. Permissions

| Action | Who |
|---|---|
| Propose task (moderated) | Group moderators, admins |
| Auto-assign | System (on case publish) |
| Self-assign | Any group consultant |
| Accept proposed task | The proposed user only |
| Decline proposed task | The proposed user only |
| Return for reassignment | Assigned user |
| Complete task | Assigned user |
| Create FollowUp task | Assigned consultant (target = case owner) |
| Cancel task (any state) | Group moderators, admins |
| View all group tasks | Group moderators, admins |
| View own tasks | The user themselves |

## 8. Domain Events

Defined but deferred for notification integration (see §10):

- `TaskAssignmentProposedEvent : ServiceRequestEvent`
- `TaskAssignmentAcceptedEvent : ServiceRequestEvent`
- `TaskAssignmentDeclinedEvent : ServiceRequestEvent`
- `TaskAssignmentReturnedEvent : ServiceRequestEvent`
- `TaskAssignmentCompletedEvent : ServiceRequestEvent`
- `TaskAssignmentCancelledEvent : ServiceRequestEvent`

These persist to the EventStore for audit trail and future notification use.

## 9. Backend Structure

```
src/core/iPath.Domain/Entities/TaskAssignment/
├── TaskAssignment.cs
├── eTaskStatus.cs
├── eTaskType.cs
├── eTaskAssignmentMode.cs
└── eTaskAssignmentStrategy.cs      # or beside GroupSettings

src/core/iPath.Application/Features/TaskAssignments/
├── Commands/
│   ├── ProposeTaskAssignmentCommand.cs
│   ├── AcceptTaskAssignmentCommand.cs
│   ├── DeclineTaskAssignmentCommand.cs
│   ├── CompleteTaskAssignmentCommand.cs
│   ├── ReturnTaskAssignmentCommand.cs
│   ├── CancelTaskAssignmentCommand.cs
│   └── CreateFollowUpTaskCommand.cs
├── Queries/
│   ├── GetUserTaskAssignmentsQuery.cs
│   ├── GetGroupTaskAssignmentsQuery.cs
│   ├── GetCaseTaskAssignmentsQuery.cs
│   └── GetTaskAssignmentByIdQuery.cs
├── Services/
│   ├── IAssignmentCandidateService.cs
│   └── AssignmentCandidateService.cs
└── Dto/
    └── TaskAssignmentDto.cs
```

Handlers follow the existing pattern in
`src/infrastructure/iPath.Database.EFCore/FeatureHandlers/`.

## 10. Notification Integration (Deferred to Phase 2)

Phase 1 ships with Task pages only — no in-app/email notifications for task events.
The domain events (§8) are still defined and raised so the event store is correct,
but the notification pipeline (SSE/Email) is not connected yet.

A `TaskAssignmentNotification` payload type can be defined for future use.

## 11. UI Components (Phase 1)

### My Tasks page
- Filterable grid of current user's tasks
- Columns: Case title, Group, Type, Status, Deadline, Accepted/Completed dates
- Actions per row: Accept, Decline, Complete, Return (depending on status)

### Group Tasks page (moderator view)
- All tasks for a group, filterable by status, assignee, type
- Actions: Cancel, Reassign (moderator selects new consultant)

### Task card in case detail view
- Shows current task assignment(s) for this case
- Quick actions: Accept, Complete, Return

### Group Settings page
- New section for Task Assignment Strategy dropdown
- Auto-assign timeout input (visible when AutoAssign selected)

### Consultant toggle
- Already exists in group admin (IsConsultant checkbox)
- No changes needed

## 12. Phase 1 Scope Summary

1. **Domain layer**: `TaskAssignment` entity + enums, `GroupSettings` extensions
2. **Data layer**: EF Core migration, DbSet, configuration, handlers
3. **Application layer**: All commands + queries + service interface + candidate service
4. **API layer**: Minimal REST endpoints (or reuse DirectApiClient pattern)
5. **UI layer**: My Tasks page, Group Tasks page, task card in case detail, settings UI

## 13. Out of Scope (Phase 1)

- SSE/Email notifications for task events
- Background worker for auto-assign timeout detection
- Automated deadline reminders
- Community-level task overview (only group-level for now)
- Integration with FinalAssessment annotation flow
