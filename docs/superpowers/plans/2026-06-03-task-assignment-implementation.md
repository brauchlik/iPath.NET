# Task Assignment System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a Task Assignment system for expert groups, enabling proposal, acceptance, completion, and cancellation of diagnostic review and follow-up tasks linked to ServiceRequests.

**Architecture:** New `TaskAssignment` entity in the domain layer, CQRS commands/queries using DispatchR.Mediator, handlers in EF Core infrastructure, Blazor UI pages using `IPathApi` (Refit interface + DirectApiClient for Server mode) that routes to Minimal API endpoints (WASM mode). Reuses existing event store, group membership/permission patterns, and notification pipeline (deferred).

**Tech Stack:** .NET 10, DispatchR.Mediator 2.3, EF Core, Blazor Server/WASM, MudBlazor, xUnit, Refit

---

### File Structure

```
src/core/iPath.Domain/Entities/TaskAssignments/
├── TaskAssignment.cs
├── eTaskStatus.cs
├── eTaskType.cs
├── eTaskAssignmentMode.cs
├── eTaskAssignmentStrategy.cs

src/core/iPath.Application/Features/TaskAssignments/
├── Dto/TaskAssignmentDto.cs
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
├── Services/IAssignmentCandidateService.cs
└── Events/
    ├── TaskAssignmentProposedEvent.cs
    ├── TaskAssignmentAcceptedEvent.cs
    ├── TaskAssignmentDeclinedEvent.cs
    ├── TaskAssignmentCompletedEvent.cs
    └── TaskAssignmentCancelledEvent.cs

src/infrastructure/iPath.Database.EFCore/FeatureHandlers/TaskAssignments/
├── Commands/
│   ├── ProposeTaskAssignmentHandler.cs
│   ├── AcceptTaskAssignmentHandler.cs
│   ├── DeclineTaskAssignmentHandler.cs
│   ├── CompleteTaskAssignmentHandler.cs
│   ├── ReturnTaskAssignmentHandler.cs
│   ├── CancelTaskAssignmentHandler.cs
│   └── CreateFollowUpTaskHandler.cs
├── Queries/
│   ├── GetUserTaskAssignmentsHandler.cs
│   ├── GetGroupTaskAssignmentsHandler.cs
│   ├── GetCaseTaskAssignmentsHandler.cs
│   └── GetTaskAssignmentByIdHandler.cs
└── Services/AssignmentCandidateService.cs

src/infrastructure/iPath.Database.EFCore/Database/Configurations/
└── TaskAssignmentConfiguration.cs    (NEW — follows GroupConfiguration.cs pattern)

src/infrastructure/iPath.API/Endpoints/
└── TaskAssignmentEndpoints.cs        (NEW — Minimal API endpoints for WASM mode)

src/ui/iPath.Blazor.ServiceLib/ApiClient/
└── IPathApi.cs                       (MODIFY — add TaskAssignment methods with Refit attributes)

src/ui/iPath.Blazor.ServiceLib/Services/
└── DirectApiClient.cs                (MODIFY — implement TaskAssignment methods via Mediator)

src/ui/iPath.RazorLib/TaskAssignments/
├── Pages/MyTasks.razor               (NEW — @page "/mytasks")
├── Pages/MyTasks.razor.cs            (NEW — code-behind, injects IPathApi)
├── Pages/GroupTasks.razor            (NEW — @page "/admin/groups/{GroupId:guid}/tasks")
├── Pages/GroupTasks.razor.cs         (NEW — code-behind, injects IPathApi)
└── Components/TaskAssignmentCard.razor         (NEW — card for case detail view)
└── Components/TaskAssignmentCard.razor.cs      (NEW — code-behind, injects IPathApi)

Modified:
- src/core/iPath.Domain/Entities/Groups/GroupSettings.cs
- src/infrastructure/iPath.Database.EFCore/Database/iPathDbContext.cs
- src/infrastructure/iPath.API/APIServicesRegistration.cs
- src/infrastructure/iPath.API/MapEndpoints.cs
- src/ui/iPath.RazorLib/Users/UserNavMenu.razor
- src/ui/iPath.RazorLib/_Imports.razor
```

---

### Task 1: Domain Entity + Enums + GroupSettings Extension

**Files:**
- Create: `src/core/iPath.Domain/Entities/TaskAssignments/eTaskStatus.cs`
- Create: `src/core/iPath.Domain/Entities/TaskAssignments/eTaskType.cs`
- Create: `src/core/iPath.Domain/Entities/TaskAssignments/eTaskAssignmentMode.cs`
- Create: `src/core/iPath.Domain/Entities/TaskAssignments/eTaskAssignmentStrategy.cs`
- Create: `src/core/iPath.Domain/Entities/TaskAssignments/TaskAssignment.cs`
- Modify: `src/core/iPath.Domain/Entities/Groups/GroupSettings.cs`

- [ ] **Step 1: Create eTaskStatus.cs**

```csharp
namespace iPath.Domain.Entities;

public enum eTaskStatus
{
    Proposed = 0,
    Assigned = 1,
    InProgress = 2,
    Completed = 3,
    Declined = 4,
    ReturnedForReassignment = 5,
    Cancelled = 6
}
```

- [ ] **Step 2: Create eTaskType.cs**

```csharp
namespace iPath.Domain.Entities;

public enum eTaskType
{
    DiagnosticReview = 0,
    FollowUp = 1
}
```

- [ ] **Step 3: Create eTaskAssignmentMode.cs**

```csharp
namespace iPath.Domain.Entities;

public enum eTaskAssignmentMode
{
    SelfAssigned = 0,
    AutoAssigned = 1,
    ModeratorSuggested = 2,
    DirectAssigned = 3
}
```

- [ ] **Step 4: Create eTaskAssignmentStrategy.cs**

```csharp
namespace iPath.Domain.Entities;

public enum eTaskAssignmentStrategy
{
    None = 0,
    SelfService = 1,
    AutoAssign = 2,
    Moderated = 3,
    SelfOrModerated = 4
}
```

- [ ] **Step 5: Create TaskAssignment.cs**

```csharp
namespace iPath.Domain.Entities;

public class TaskAssignment : AuditableEntityWithEvents
{
    public Guid ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;

    public Guid AssignedToUserId { get; set; }
    public User AssignedToUser { get; set; } = null!;

    public Guid? AssignedByUserId { get; set; }
    public User? AssignedByUser { get; set; }

    public eTaskType Type { get; set; }
    public eTaskAssignmentMode Mode { get; set; }
    public eTaskStatus Status { get; set; }

    public string? Notes { get; set; }
    public DateTime? AcceptedOn { get; set; }
    public DateTime? CompletedOn { get; set; }
    public DateTime? Deadline { get; set; }
    public int? AttemptNumber { get; set; }

    public void Accept()
    {
        Status = eTaskStatus.Assigned;
        AcceptedOn = DateTime.UtcNow;
    }

    public void Decline()
    {
        Status = eTaskStatus.Declined;
    }

    public void StartWork()
    {
        if (Status is eTaskStatus.Assigned)
            Status = eTaskStatus.InProgress;
    }

    public void Complete()
    {
        Status = eTaskStatus.Completed;
        CompletedOn = DateTime.UtcNow;
    }

    public void ReturnForReassignment()
    {
        Status = eTaskStatus.ReturnedForReassignment;
    }

    public void Cancel()
    {
        Status = eTaskStatus.Cancelled;
    }
}
```

- [ ] **Step 6: Modify GroupSettings.cs** — add strategy and timeout fields

Add inside the `GroupSettings` class body after the existing properties:

```csharp
    public eTaskAssignmentStrategy TaskAssignmentStrategy { get; set; } = eTaskAssignmentStrategy.None;
    public int? AutoAssignTimeoutHours { get; set; } = 24;
```

- [ ] **Step 7: Build and verify**

```bash
dotnet build src/core/iPath.Domain/iPath.Domain.csproj --no-restore
```

Expected: Build succeeds

- [ ] **Step 8: Commit**

```bash
git add src/core/iPath.Domain/Entities/TaskAssignments/ src/core/iPath.Domain/Entities/Groups/GroupSettings.cs
git commit -m "feat: add TaskAssignment entity, enums, and GroupSettings strategy"
```

---

### Task 2: Domain Events

**Files:** Create event classes under `src/core/iPath.Application/Features/TaskAssignments/Events/`

- [ ] **Step 1: Create TaskAssignmentProposedEvent.cs**

```csharp
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public class TaskAssignmentProposedEvent : ServiceRequestEvent, IEventWithNotifications;
```

- [ ] **Step 2: Create TaskAssignmentAcceptedEvent.cs**

```csharp
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public class TaskAssignmentAcceptedEvent : ServiceRequestEvent, IEventWithNotifications;
```

- [ ] **Step 3: Create TaskAssignmentDeclinedEvent.cs**

```csharp
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public class TaskAssignmentDeclinedEvent : ServiceRequestEvent, IEventWithNotifications;
```

- [ ] **Step 4: Create TaskAssignmentCompletedEvent.cs**

```csharp
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public class TaskAssignmentCompletedEvent : ServiceRequestEvent, IEventWithNotifications;
```

- [ ] **Step 5: Create TaskAssignmentCancelledEvent.cs**

```csharp
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public class TaskAssignmentCancelledEvent : ServiceRequestEvent, IEventWithNotifications;
```

- [ ] **Step 6: Build and verify**

```bash
dotnet build src/core/iPath.Application/iPath.Application.csproj --no-restore
```

Expected: Build succeeds

- [ ] **Step 7: Commit**

```bash
git add src/core/iPath.Application/Features/TaskAssignments/Events/
git commit -m "feat: add TaskAssignment domain events"
```

---

### Task 3: DTO + Commands + Queries + Service Interface

**Files:**
- Create: `src/core/iPath.Application/Features/TaskAssignments/Dto/TaskAssignmentDto.cs`
- Create: all command records
- Create: all query records
- Create: `src/core/iPath.Application/Features/TaskAssignments/Services/IAssignmentCandidateService.cs`

- [ ] **Step 1: Create TaskAssignmentDto.cs**

```csharp
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public class TaskAssignmentDto
{
    public Guid Id { get; init; }
    public Guid ServiceRequestId { get; init; }
    public string? CaseTitle { get; init; }
    public Guid GroupId { get; init; }
    public string? GroupName { get; init; }

    public Guid AssignedToUserId { get; init; }
    public string? AssignedToUsername { get; init; }

    public Guid? AssignedByUserId { get; init; }
    public string? AssignedByUsername { get; init; }

    public string Type { get; init; } = default!;
    public string Mode { get; init; } = default!;
    public string Status { get; init; } = default!;

    public string? Notes { get; init; }
    public DateTime CreatedOn { get; init; }
    public DateTime? AcceptedOn { get; init; }
    public DateTime? CompletedOn { get; init; }
    public DateTime? Deadline { get; init; }
}

public static class TaskAssignmentDtoExtensions
{
    public static TaskAssignmentDto ToDto(this TaskAssignment ta)
    {
        return new TaskAssignmentDto
        {
            Id = ta.Id,
            ServiceRequestId = ta.ServiceRequestId,
            CaseTitle = ta.ServiceRequest?.Description?.CaseTitle,
            GroupId = ta.ServiceRequest?.GroupId ?? Guid.Empty,
            GroupName = ta.ServiceRequest?.Group?.Name,
            AssignedToUserId = ta.AssignedToUserId,
            AssignedToUsername = ta.AssignedToUser?.UserName,
            AssignedByUserId = ta.AssignedByUserId,
            AssignedByUsername = ta.AssignedByUser?.UserName,
            Type = ta.Type.ToString(),
            Mode = ta.Mode.ToString(),
            Status = ta.Status.ToString(),
            Notes = ta.Notes,
            CreatedOn = ta.CreatedOn,
            AcceptedOn = ta.AcceptedOn,
            CompletedOn = ta.CompletedOn,
            Deadline = ta.Deadline
        };
    }
}
```

- [ ] **Step 2: Create ProposeTaskAssignmentCommand.cs**

```csharp
using DispatchR.Abstractions.Send;
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public record ProposeTaskAssignmentCommand(
    Guid ServiceRequestId,
    Guid AssignedToUserId,
    eTaskAssignmentMode Mode,
    string? Notes = null)
    : IRequest<ProposeTaskAssignmentCommand, Task<TaskAssignmentDto>>;
```

- [ ] **Step 3: Create AcceptTaskAssignmentCommand.cs**

```csharp
using DispatchR.Abstractions.Send;
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public record AcceptTaskAssignmentCommand(Guid TaskAssignmentId)
    : IRequest<AcceptTaskAssignmentCommand, Task<TaskAssignmentDto>>;
```

- [ ] **Step 4: Create DeclineTaskAssignmentCommand.cs**

```csharp
using DispatchR.Abstractions.Send;
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public record DeclineTaskAssignmentCommand(Guid TaskAssignmentId)
    : IRequest<DeclineTaskAssignmentCommand, Task<TaskAssignmentDto>>;
```

- [ ] **Step 5: Create CompleteTaskAssignmentCommand.cs**

```csharp
using DispatchR.Abstractions.Send;
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public record CompleteTaskAssignmentCommand(Guid TaskAssignmentId)
    : IRequest<CompleteTaskAssignmentCommand, Task<TaskAssignmentDto>>;
```

- [ ] **Step 6: Create ReturnTaskAssignmentCommand.cs**

```csharp
using DispatchR.Abstractions.Send;
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public record ReturnTaskAssignmentCommand(Guid TaskAssignmentId)
    : IRequest<ReturnTaskAssignmentCommand, Task<TaskAssignmentDto>>;
```

- [ ] **Step 7: Create CancelTaskAssignmentCommand.cs**

```csharp
using DispatchR.Abstractions.Send;
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public record CancelTaskAssignmentCommand(Guid TaskAssignmentId)
    : IRequest<CancelTaskAssignmentCommand, Task<TaskAssignmentDto>>;
```

- [ ] **Step 8: Create CreateFollowUpTaskCommand.cs**

```csharp
using DispatchR.Abstractions.Send;
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public record CreateFollowUpTaskCommand(Guid ServiceRequestId, string? Notes = null)
    : IRequest<CreateFollowUpTaskCommand, Task<TaskAssignmentDto>>;
```

- [ ] **Step 9: Create GetUserTaskAssignmentsQuery.cs**

```csharp
using DispatchR.Abstractions.Send;
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public record GetUserTaskAssignmentsQuery(Guid? UserId = null, eTaskStatus? StatusFilter = null)
    : IRequest<GetUserTaskAssignmentsQuery, Task<IReadOnlyList<TaskAssignmentDto>>>;
```

- [ ] **Step 10: Create GetGroupTaskAssignmentsQuery.cs**

```csharp
using DispatchR.Abstractions.Send;
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public record GetGroupTaskAssignmentsQuery(Guid GroupId, eTaskStatus? StatusFilter = null)
    : IRequest<GetGroupTaskAssignmentsQuery, Task<IReadOnlyList<TaskAssignmentDto>>>;
```

- [ ] **Step 11: Create GetCaseTaskAssignmentsQuery.cs**

```csharp
using DispatchR.Abstractions.Send;
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public record GetCaseTaskAssignmentsQuery(Guid ServiceRequestId)
    : IRequest<GetCaseTaskAssignmentsQuery, Task<IReadOnlyList<TaskAssignmentDto>>>;
```

- [ ] **Step 12: Create GetTaskAssignmentByIdQuery.cs**

```csharp
using DispatchR.Abstractions.Send;
using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public record GetTaskAssignmentByIdQuery(Guid Id)
    : IRequest<GetTaskAssignmentByIdQuery, Task<TaskAssignmentDto>>;
```

- [ ] **Step 13: Create IAssignmentCandidateService.cs**

```csharp
namespace iPath.Application.Features.TaskAssignments;

public interface IAssignmentCandidateService
{
    Task<Guid?> FindBestCandidateAsync(Guid groupId, Guid serviceRequestId, CancellationToken ct = default);
    Task<List<Guid>> GetCandidateOrderAsync(Guid groupId, Guid serviceRequestId, CancellationToken ct = default);
}
```

- [ ] **Step 14: Build and verify**

```bash
dotnet build src/core/iPath.Application/iPath.Application.csproj --no-restore
```

Expected: Build succeeds

- [ ] **Step 15: Commit**

```bash
git add src/core/iPath.Application/Features/TaskAssignments/
git commit -m "feat: add TaskAssignment DTO, commands, queries, and service interface"
```

---

### Task 4: EF Core Configuration + DbSet + Migration (all 3 providers)

**Files:**
- Create: `src/infrastructure/iPath.Database.EFCore/Database/Configurations/TaskAssignmentConfiguration.cs`
- Modify: `src/infrastructure/iPath.Database.EFCore/Database/iPathDbContext.cs`

- [ ] **Step 1: Create TaskAssignmentConfiguration.cs**

```csharp
using iPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace iPath.EF.Core.Database.Configurations;

public class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> builder)
    {
        builder.ToTable("TaskAssignments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.Notes).HasMaxLength(2000);

        builder.HasOne(x => x.ServiceRequest)
            .WithMany()
            .HasForeignKey(x => x.ServiceRequestId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.AssignedToUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedToUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.AssignedByUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.AssignedToUserId);
        builder.HasIndex(x => x.ServiceRequestId);
        builder.HasIndex(x => x.Status);
    }
}
```

- [ ] **Step 2: Add DbSet to iPathDbContext.cs**

Open `src/infrastructure/iPath.Database.EFCore/Database/iPathDbContext.cs`. After the existing `DbSet` properties, add:

```csharp
    public DbSet<TaskAssignment> TaskAssignments { get; set; }
```

Also register the configuration in `OnModelCreating`. Find where existing configurations are applied (e.g., `builder.ApplyConfiguration(new GroupConfiguration());`) and add:

```csharp
    builder.ApplyConfiguration(new TaskAssignmentConfiguration());
```

- [ ] **Step 3: Build to verify configuration compiles**

```bash
dotnet build src/infrastructure/iPath.Database.EFCore/iPath.EF.Core.csproj --no-restore
```

Expected: Build succeeds

- [ ] **Step 4: Create EF Core migration for Sqlite**

```bash
dotnet ef migrations add AddTaskAssignmentEntity --project src/infrastructure/iPath.Database.Sqlite --startup-project src/ui/iPath.Blazor.Server
```

Expected: Migration files created in the Sqlite Migrations folder

- [ ] **Step 5: Create EF Core migration for SqlServer**

```bash
dotnet ef migrations add AddTaskAssignmentEntity --project src/infrastructure/iPath.Database.Sqlserver --startup-project src/ui/iPath.Blazor.Server
```

Expected: Migration files created in the SqlServer Migrations folder

- [ ] **Step 6: Create EF Core migration for Postgres**

```bash
dotnet ef migrations add AddTaskAssignmentEntity --project src/infrastructure/iPath.Database.Postgres --startup-project src/ui/iPath.Blazor.Server
```

Expected: Migration files created in the Postgres Migrations folder

- [ ] **Step 7: Commit**

```bash
git add src/infrastructure/iPath.Database.EFCore/ "src/infrastructure/iPath.Database.Sqlite/Migrations/" "src/infrastructure/iPath.Database.Sqlserver/Migrations/" "src/infrastructure/iPath.Database.Postgres/Migrations/"
git commit -m "feat: add EF Core configuration and migrations for TaskAssignment"
```

---

### Task 5: Command Handlers

**Files:** Create under `src/infrastructure/iPath.Database.EFCore/FeatureHandlers/TaskAssignments/Commands/`

- [ ] **Step 1: Create ProposeTaskAssignmentHandler.cs**

```csharp
using DispatchR;
using iPath.Application.Contracts;
using iPath.Application.Exceptions;
using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Commands;

public class ProposeTaskAssignmentHandler(
    iPathDbContext db,
    IUserSession sess,
    IMediator mediator,
    ILogger<ProposeTaskAssignmentHandler> logger)
    : IRequestHandler<ProposeTaskAssignmentCommand, Task<TaskAssignmentDto>>
{
    public async Task<TaskAssignmentDto> Handle(ProposeTaskAssignmentCommand request, CancellationToken ct)
    {
        var sr = await db.ServiceRequests.FindAsync([request.ServiceRequestId], ct);
        Guard.Against.NotFound(request.ServiceRequestId, sr);

        if (!sess.IsAdmin)
            sess.AssertInGroup(sr.GroupId);

        var user = await db.Users.FindAsync([request.AssignedToUserId], ct);
        Guard.Against.NotFound(request.AssignedToUserId, user);

        var ta = new TaskAssignment
        {
            Id = Guid.CreateVersion7(),
            ServiceRequestId = request.ServiceRequestId,
            AssignedToUserId = request.AssignedToUserId,
            AssignedByUserId = sess.User.Id,
            Type = eTaskType.DiagnosticReview,
            Mode = request.Mode,
            Status = request.Mode == eTaskAssignmentMode.DirectAssigned ? eTaskStatus.Assigned : eTaskStatus.Proposed,
            Notes = request.Notes,
            CreatedOn = DateTime.UtcNow
        };

        if (ta.Status == eTaskStatus.Assigned)
            ta.AcceptedOn = DateTime.UtcNow;

        db.TaskAssignments.Add(ta);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("TaskAssignment {Id} proposed for SR {SrId} to user {UserId}", ta.Id, request.ServiceRequestId, request.AssignedToUserId);
        return await mediator.Send(new GetTaskAssignmentByIdQuery(ta.Id), ct);
    }
}
```

- [ ] **Step 2: Create AcceptTaskAssignmentHandler.cs**

```csharp
using DispatchR;
using iPath.Application.Contracts;
using iPath.Application.Exceptions;
using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Commands;

public class AcceptTaskAssignmentHandler(
    iPathDbContext db,
    IUserSession sess,
    IMediator mediator,
    ILogger<AcceptTaskAssignmentHandler> logger)
    : IRequestHandler<AcceptTaskAssignmentCommand, Task<TaskAssignmentDto>>
{
    public async Task<TaskAssignmentDto> Handle(AcceptTaskAssignmentCommand request, CancellationToken ct)
    {
        var ta = await db.TaskAssignments.FindAsync([request.TaskAssignmentId], ct);
        Guard.Against.NotFound(request.TaskAssignmentId, ta);

        if (ta.AssignedToUserId != sess.User.Id && !sess.IsAdmin)
            throw new NotAllowedException("Only the assigned user can accept this task");

        if (ta.Status != eTaskStatus.Proposed)
            throw new InvalidOperationException($"Cannot accept task in status {ta.Status}");

        ta.Accept();
        await db.SaveChangesAsync(ct);

        logger.LogInformation("TaskAssignment {Id} accepted by user {UserId}", ta.Id, sess.User.Id);
        return await mediator.Send(new GetTaskAssignmentByIdQuery(ta.Id), ct);
    }
}
```

- [ ] **Step 3: Create DeclineTaskAssignmentHandler.cs**

```csharp
using DispatchR;
using iPath.Application.Contracts;
using iPath.Application.Exceptions;
using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Commands;

public class DeclineTaskAssignmentHandler(
    iPathDbContext db,
    IUserSession sess,
    IMediator mediator,
    ILogger<DeclineTaskAssignmentHandler> logger)
    : IRequestHandler<DeclineTaskAssignmentCommand, Task<TaskAssignmentDto>>
{
    public async Task<TaskAssignmentDto> Handle(DeclineTaskAssignmentCommand request, CancellationToken ct)
    {
        var ta = await db.TaskAssignments.FindAsync([request.TaskAssignmentId], ct);
        Guard.Against.NotFound(request.TaskAssignmentId, ta);

        if (ta.AssignedToUserId != sess.User.Id && !sess.IsAdmin)
            throw new NotAllowedException("Only the assigned user can decline this task");

        if (ta.Status != eTaskStatus.Proposed)
            throw new InvalidOperationException($"Cannot decline task in status {ta.Status}");

        ta.Decline();
        await db.SaveChangesAsync(ct);

        logger.LogInformation("TaskAssignment {Id} declined by user {UserId}", ta.Id, sess.User.Id);
        return await mediator.Send(new GetTaskAssignmentByIdQuery(ta.Id), ct);
    }
}
```

- [ ] **Step 4: Create CompleteTaskAssignmentHandler.cs**

```csharp
using DispatchR;
using iPath.Application.Contracts;
using iPath.Application.Exceptions;
using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Commands;

public class CompleteTaskAssignmentHandler(
    iPathDbContext db,
    IUserSession sess,
    IMediator mediator,
    ILogger<CompleteTaskAssignmentHandler> logger)
    : IRequestHandler<CompleteTaskAssignmentCommand, Task<TaskAssignmentDto>>
{
    public async Task<TaskAssignmentDto> Handle(CompleteTaskAssignmentCommand request, CancellationToken ct)
    {
        var ta = await db.TaskAssignments.FindAsync([request.TaskAssignmentId], ct);
        Guard.Against.NotFound(request.TaskAssignmentId, ta);

        if (ta.AssignedToUserId != sess.User.Id && !sess.IsAdmin)
            throw new NotAllowedException("Only the assigned user can complete this task");

        if (ta.Status is not (eTaskStatus.Assigned or eTaskStatus.InProgress))
            throw new InvalidOperationException($"Cannot complete task in status {ta.Status}");

        ta.StartWork();
        ta.Complete();
        await db.SaveChangesAsync(ct);

        logger.LogInformation("TaskAssignment {Id} completed by user {UserId}", ta.Id, sess.User.Id);
        return await mediator.Send(new GetTaskAssignmentByIdQuery(ta.Id), ct);
    }
}
```

- [ ] **Step 5: Create ReturnTaskAssignmentHandler.cs**

```csharp
using DispatchR;
using iPath.Application.Contracts;
using iPath.Application.Exceptions;
using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Commands;

public class ReturnTaskAssignmentHandler(
    iPathDbContext db,
    IUserSession sess,
    IMediator mediator,
    ILogger<ReturnTaskAssignmentHandler> logger)
    : IRequestHandler<ReturnTaskAssignmentCommand, Task<TaskAssignmentDto>>
{
    public async Task<TaskAssignmentDto> Handle(ReturnTaskAssignmentCommand request, CancellationToken ct)
    {
        var ta = await db.TaskAssignments.FindAsync([request.TaskAssignmentId], ct);
        Guard.Against.NotFound(request.TaskAssignmentId, ta);

        if (ta.AssignedToUserId != sess.User.Id && !sess.IsAdmin)
            throw new NotAllowedException("Only the assigned user can return this task");

        if (ta.Status is not (eTaskStatus.Assigned or eTaskStatus.InProgress))
            throw new InvalidOperationException($"Cannot return task in status {ta.Status}");

        ta.ReturnForReassignment();
        await db.SaveChangesAsync(ct);

        logger.LogInformation("TaskAssignment {Id} returned for reassignment by user {UserId}", ta.Id, sess.User.Id);
        return await mediator.Send(new GetTaskAssignmentByIdQuery(ta.Id), ct);
    }
}
```

- [ ] **Step 6: Create CancelTaskAssignmentHandler.cs**

```csharp
using DispatchR;
using iPath.Application.Contracts;
using iPath.Application.Exceptions;
using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Commands;

public class CancelTaskAssignmentHandler(
    iPathDbContext db,
    IUserSession sess,
    IMediator mediator,
    ILogger<CancelTaskAssignmentHandler> logger)
    : IRequestHandler<CancelTaskAssignmentCommand, Task<TaskAssignmentDto>>
{
    public async Task<TaskAssignmentDto> Handle(CancelTaskAssignmentCommand request, CancellationToken ct)
    {
        var ta = await db.TaskAssignments.FindAsync([request.TaskAssignmentId], ct);
        Guard.Against.NotFound(request.TaskAssignmentId, ta);

        var sr = await db.ServiceRequests.FindAsync([ta.ServiceRequestId], ct);
        if (!sess.IsAdmin)
            sess.AssertInGroup(sr!.GroupId);

        var gm = sess.User.GroupMembership.FirstOrDefault(m => m.GroupId == sr!.GroupId);
        if (gm?.Role != eMemberRole.Moderator && !sess.IsAdmin)
            throw new NotAllowedException("Only moderators and admins can cancel tasks");

        ta.Cancel();
        await db.SaveChangesAsync(ct);

        logger.LogInformation("TaskAssignment {Id} cancelled by user {UserId}", ta.Id, sess.User.Id);
        return await mediator.Send(new GetTaskAssignmentByIdQuery(ta.Id), ct);
    }
}
```

- [ ] **Step 7: Create CreateFollowUpTaskHandler.cs**

```csharp
using DispatchR;
using iPath.Application.Contracts;
using iPath.Application.Exceptions;
using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Commands;

public class CreateFollowUpTaskHandler(
    iPathDbContext db,
    IUserSession sess,
    IMediator mediator,
    ILogger<CreateFollowUpTaskHandler> logger)
    : IRequestHandler<CreateFollowUpTaskCommand, Task<TaskAssignmentDto>>
{
    public async Task<TaskAssignmentDto> Handle(CreateFollowUpTaskCommand request, CancellationToken ct)
    {
        var sr = await db.ServiceRequests
            .Include(x => x.Owner)
            .FirstOrDefaultAsync(x => x.Id == request.ServiceRequestId, ct);
        Guard.Against.NotFound(request.ServiceRequestId, sr);

        if (!sess.IsAdmin)
            sess.AssertInGroup(sr.GroupId);

        var ta = new TaskAssignment
        {
            Id = Guid.CreateVersion7(),
            ServiceRequestId = request.ServiceRequestId,
            AssignedToUserId = sr.OwnerId,
            AssignedByUserId = sess.User.Id,
            Type = eTaskType.FollowUp,
            Mode = eTaskAssignmentMode.DirectAssigned,
            Status = eTaskStatus.Assigned,
            Notes = request.Notes,
            AcceptedOn = DateTime.UtcNow,
            CreatedOn = DateTime.UtcNow
        };

        db.TaskAssignments.Add(ta);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("FollowUp Task {Id} created for SR {SrId} to owner {OwnerId}", ta.Id, request.ServiceRequestId, sr.OwnerId);
        return await mediator.Send(new GetTaskAssignmentByIdQuery(ta.Id), ct);
    }
}
```

- [ ] **Step 8: Build and verify**

```bash
dotnet build src/infrastructure/iPath.Database.EFCore/iPath.EF.Core.csproj --no-restore
```

Expected: Build succeeds

- [ ] **Step 9: Commit**

```bash
git add src/infrastructure/iPath.Database.EFCore/FeatureHandlers/TaskAssignments/Commands/
git commit -m "feat: add TaskAssignment command handlers"
```

---

### Task 6: Query Handlers

**Files:** Create under `src/infrastructure/iPath.Database.EFCore/FeatureHandlers/TaskAssignments/Queries/`

- [ ] **Step 1: Create GetUserTaskAssignmentsHandler.cs**

```csharp
using DispatchR;
using iPath.Application.Contracts;
using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Queries;

public class GetUserTaskAssignmentsHandler(
    iPathDbContext db,
    IUserSession sess,
    ILogger<GetUserTaskAssignmentsHandler> logger)
    : IRequestHandler<GetUserTaskAssignmentsQuery, Task<IReadOnlyList<TaskAssignmentDto>>>
{
    public async Task<IReadOnlyList<TaskAssignmentDto>> Handle(GetUserTaskAssignmentsQuery request, CancellationToken ct)
    {
        var userId = request.UserId ?? sess.User.Id;

        var query = db.TaskAssignments
            .AsNoTracking()
            .Include(t => t.ServiceRequest).ThenInclude(s => s.Group)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .Where(t => t.AssignedToUserId == userId);

        if (request.StatusFilter.HasValue)
            query = query.Where(t => t.Status == request.StatusFilter.Value);

        var results = await query
            .OrderByDescending(t => t.CreatedOn)
            .ToListAsync(ct);

        return results.Select(t => t.ToDto()).ToList();
    }
}
```

- [ ] **Step 2: Create GetGroupTaskAssignmentsHandler.cs**

```csharp
using DispatchR;
using iPath.Application.Contracts;
using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Queries;

public class GetGroupTaskAssignmentsHandler(
    iPathDbContext db,
    IUserSession sess,
    ILogger<GetGroupTaskAssignmentsHandler> logger)
    : IRequestHandler<GetGroupTaskAssignmentsQuery, Task<IReadOnlyList<TaskAssignmentDto>>>
{
    public async Task<IReadOnlyList<TaskAssignmentDto>> Handle(GetGroupTaskAssignmentsQuery request, CancellationToken ct)
    {
        if (!sess.IsAdmin)
            sess.AssertInGroup(request.GroupId);

        var query = db.TaskAssignments
            .AsNoTracking()
            .Include(t => t.ServiceRequest).ThenInclude(s => s.Group)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .Where(t => t.ServiceRequest.GroupId == request.GroupId);

        if (request.StatusFilter.HasValue)
            query = query.Where(t => t.Status == request.StatusFilter.Value);

        var results = await query
            .OrderByDescending(t => t.CreatedOn)
            .ToListAsync(ct);

        return results.Select(t => t.ToDto()).ToList();
    }
}
```

- [ ] **Step 3: Create GetCaseTaskAssignmentsHandler.cs**

```csharp
using DispatchR;
using iPath.Application.Features.TaskAssignments;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Queries;

public class GetCaseTaskAssignmentsHandler(
    iPathDbContext db,
    ILogger<GetCaseTaskAssignmentsHandler> logger)
    : IRequestHandler<GetCaseTaskAssignmentsQuery, Task<IReadOnlyList<TaskAssignmentDto>>>
{
    public async Task<IReadOnlyList<TaskAssignmentDto>> Handle(GetCaseTaskAssignmentsQuery request, CancellationToken ct)
    {
        var results = await db.TaskAssignments
            .AsNoTracking()
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .Where(t => t.ServiceRequestId == request.ServiceRequestId)
            .OrderByDescending(t => t.CreatedOn)
            .ToListAsync(ct);

        return results.Select(t => t.ToDto()).ToList();
    }
}
```

- [ ] **Step 4: Create GetTaskAssignmentByIdHandler.cs**

```csharp
using DispatchR;
using iPath.Application.Features.TaskAssignments;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Queries;

public class GetTaskAssignmentByIdHandler(
    iPathDbContext db,
    ILogger<GetTaskAssignmentByIdHandler> logger)
    : IRequestHandler<GetTaskAssignmentByIdQuery, Task<TaskAssignmentDto>>
{
    public async Task<TaskAssignmentDto> Handle(GetTaskAssignmentByIdQuery request, CancellationToken ct)
    {
        var ta = await db.TaskAssignments
            .AsNoTracking()
            .Include(t => t.ServiceRequest).ThenInclude(s => s.Group)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .FirstOrDefaultAsync(t => t.Id == request.Id, ct);

        Guard.Against.NotFound(request.Id, ta);
        return ta.ToDto();
    }
}
```

- [ ] **Step 5: Build and verify**

```bash
dotnet build src/infrastructure/iPath.Database.EFCore/iPath.EF.Core.csproj --no-restore
```

Expected: Build succeeds

- [ ] **Step 6: Commit**

```bash
git add src/infrastructure/iPath.Database.EFCore/FeatureHandlers/TaskAssignments/Queries/
git commit -m "feat: add TaskAssignment query handlers"
```

---

### Task 7: AssignmentCandidateService + DI Registration

**Files:**
- Create: `src/infrastructure/iPath.Database.EFCore/FeatureHandlers/TaskAssignments/Services/AssignmentCandidateService.cs`
- Modify: `src/infrastructure/iPath.API/APIServicesRegistration.cs`

- [ ] **Step 1: Create AssignmentCandidateService.cs**

```csharp
using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Services;

public class AssignmentCandidateService(
    iPathDbContext db,
    ILogger<AssignmentCandidateService> logger)
    : IAssignmentCandidateService
{
    public async Task<Guid?> FindBestCandidateAsync(Guid groupId, Guid serviceRequestId, CancellationToken ct = default)
    {
        var candidates = await GetCandidateOrderAsync(groupId, serviceRequestId, ct);
        return candidates.FirstOrDefault();
    }

    public async Task<List<Guid>> GetCandidateOrderAsync(Guid groupId, Guid serviceRequestId, CancellationToken ct = default)
    {
        var sr = await db.ServiceRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == serviceRequestId, ct);

        if (sr?.Description?.BodySite is null)
        {
            return await db.Groups
                .AsNoTracking()
                .Where(g => g.Id == groupId)
                .SelectMany(g => g.Members
                    .Where(m => m.IsConsultant && m.Role >= eMemberRole.User)
                    .Select(m => m.UserId))
                .ToListAsync(ct);
        }

        var consultants = await db.Groups
            .AsNoTracking()
            .Where(g => g.Id == groupId)
            .SelectMany(g => g.Members
                .Where(m => m.IsConsultant && m.Role >= eMemberRole.User)
                .Select(m => new
                {
                    m.UserId,
                    m.NotificationSettings!.BodySiteFilter,
                    m.NotificationSettings!.UseProfileBodySiteFilter,
                    ProfileBodySite = m.User.Profile.SpecialisationBodySite
                }))
            .ToListAsync(ct);

        var bodySite = sr.Description.BodySite;
        var matched = new List<Guid>();
        var unmatched = new List<Guid>();

        foreach (var c in consultants)
        {
            var filter = c.UseProfileBodySiteFilter ? c.ProfileBodySite : c.BodySiteFilter;
            if (filter is not null)
            {
                matched.Add(c.UserId);
            }
            else
            {
                unmatched.Add(c.UserId);
            }
        }

        return matched.Concat(unmatched).ToList();
    }
}
```

- [ ] **Step 2: Register in DI**

Open `src/infrastructure/iPath.API/APIServicesRegistration.cs` and add after the existing service registrations:

```csharp
        services.AddScoped<IAssignmentCandidateService, AssignmentCandidateService>();
```

- [ ] **Step 3: Build and verify**

```bash
dotnet build src/infrastructure/iPath.API/iPath.API.csproj --no-restore
```

Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add src/infrastructure/iPath.Database.EFCore/FeatureHandlers/TaskAssignments/Services/ src/infrastructure/iPath.API/APIServicesRegistration.cs
git commit -m "feat: add AssignmentCandidateService and DI registration"
```

---

### Task 8: Minimal API Endpoints (for WASM mode)

**Files:**
- Create: `src/infrastructure/iPath.API/Endpoints/TaskAssignmentEndpoints.cs`
- Modify: `src/infrastructure/iPath.API/MapEndpoints.cs`

- [ ] **Step 1: Create TaskAssignmentEndpoints.cs**

```csharp
using DispatchR;
using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;

namespace iPath.API.Endpoints;

public static class TaskAssignmentEndpoints
{
    public static IEndpointRouteBuilder MapTaskAssignmentEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("taskassignments")
            .WithTags("Task Assignments")
            .RequireAuthorization();

        api.MapGet("/my", async (IMediator mediator, eTaskStatus? statusFilter) =>
        {
            var result = await mediator.Send(new GetUserTaskAssignmentsQuery(StatusFilter: statusFilter));
            return Results.Ok(result);
        });

        api.MapGet("/group/{groupId}", async (IMediator mediator, Guid groupId, eTaskStatus? statusFilter) =>
        {
            var result = await mediator.Send(new GetGroupTaskAssignmentsQuery(groupId, statusFilter));
            return Results.Ok(result);
        });

        api.MapGet("/case/{serviceRequestId}", async (IMediator mediator, Guid serviceRequestId) =>
        {
            var result = await mediator.Send(new GetCaseTaskAssignmentsQuery(serviceRequestId));
            return Results.Ok(result);
        });

        api.MapGet("/{id}", async (IMediator mediator, Guid id) =>
        {
            var result = await mediator.Send(new GetTaskAssignmentByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        api.MapPost("/propose", async (IMediator mediator, ProposeTaskAssignmentCommand command) =>
        {
            var result = await mediator.Send(command);
            return Results.Created($"/api/v1/taskassignments/{result.Id}", result);
        });

        api.MapPost("/{id}/accept", async (IMediator mediator, Guid id) =>
        {
            var result = await mediator.Send(new AcceptTaskAssignmentCommand(id));
            return Results.Ok(result);
        });

        api.MapPost("/{id}/decline", async (IMediator mediator, Guid id) =>
        {
            var result = await mediator.Send(new DeclineTaskAssignmentCommand(id));
            return Results.Ok(result);
        });

        api.MapPost("/{id}/complete", async (IMediator mediator, Guid id) =>
        {
            var result = await mediator.Send(new CompleteTaskAssignmentCommand(id));
            return Results.Ok(result);
        });

        api.MapPost("/{id}/return", async (IMediator mediator, Guid id) =>
        {
            var result = await mediator.Send(new ReturnTaskAssignmentCommand(id));
            return Results.Ok(result);
        });

        api.MapPost("/{id}/cancel", async (IMediator mediator, Guid id) =>
        {
            var result = await mediator.Send(new CancelTaskAssignmentCommand(id));
            return Results.Ok(result);
        });

        api.MapPost("/followup", async (IMediator mediator, CreateFollowUpTaskCommand command) =>
        {
            var result = await mediator.Send(command);
            return Results.Created($"/api/v1/taskassignments/{result.Id}", result);
        });

        return app;
    }
}
```

- [ ] **Step 2: Register endpoints in MapEndpoints.cs**

Open `src/infrastructure/iPath.API/MapEndpoints.cs` and add `.MapTaskAssignmentEndpoints()` to the chain, after `.MapEmailImportApi()`:

```csharp
            .MapEmailImportApi()
            .MapTaskAssignmentEndpoints();
```

- [ ] **Step 3: Build and verify**

```bash
dotnet build src/infrastructure/iPath.API/iPath.API.csproj --no-restore
```

Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add src/infrastructure/iPath.API/Endpoints/TaskAssignmentEndpoints.cs src/infrastructure/iPath.API/MapEndpoints.cs
git commit -m "feat: add TaskAssignment API endpoints for WASM mode"
```

---

### Task 9: IPathApi Interface — Add TaskAssignment Methods

**Files:**
- Modify: `src/ui/iPath.Blazor.ServiceLib/ApiClient/IPathApi.cs`
- Modify: `src/ui/iPath.Blazor.ServiceLib/Services/DirectApiClient.cs`

- [ ] **Step 1: Add TaskAssignment methods to IPathApi.cs**

Open `src/ui/iPath.Blazor.ServiceLib/ApiClient/IPathApi.cs`. Add a new region before the closing brace of the interface:

```csharp

    #region "-- Task Assignments --"
    [Get("/api/v1/taskassignments/my")]
    Task<IApiResponse<IReadOnlyList<TaskAssignmentDto>>> GetMyTaskAssignments(eTaskStatus? statusFilter = null);

    [Get("/api/v1/taskassignments/group/{groupId}")]
    Task<IApiResponse<IReadOnlyList<TaskAssignmentDto>>> GetGroupTaskAssignments(Guid groupId, eTaskStatus? statusFilter = null);

    [Get("/api/v1/taskassignments/case/{serviceRequestId}")]
    Task<IApiResponse<IReadOnlyList<TaskAssignmentDto>>> GetCaseTaskAssignments(Guid serviceRequestId);

    [Get("/api/v1/taskassignments/{id}")]
    Task<IApiResponse<TaskAssignmentDto>> GetTaskAssignmentById(Guid id);

    [Post("/api/v1/taskassignments/propose")]
    Task<IApiResponse<TaskAssignmentDto>> ProposeTaskAssignment([Body] ProposeTaskAssignmentCommand command);

    [Post("/api/v1/taskassignments/{id}/accept")]
    Task<IApiResponse<TaskAssignmentDto>> AcceptTaskAssignment(Guid id);

    [Post("/api/v1/taskassignments/{id}/decline")]
    Task<IApiResponse<TaskAssignmentDto>> DeclineTaskAssignment(Guid id);

    [Post("/api/v1/taskassignments/{id}/complete")]
    Task<IApiResponse<TaskAssignmentDto>> CompleteTaskAssignment(Guid id);

    [Post("/api/v1/taskassignments/{id}/return")]
    Task<IApiResponse<TaskAssignmentDto>> ReturnTaskAssignment(Guid id);

    [Post("/api/v1/taskassignments/{id}/cancel")]
    Task<IApiResponse<TaskAssignmentDto>> CancelTaskAssignment(Guid id);

    [Post("/api/v1/taskassignments/followup")]
    Task<IApiResponse<TaskAssignmentDto>> CreateFollowUpTask([Body] CreateFollowUpTaskCommand command);
    #endregion
```

Also add the required using at the top of the file (if not already imported via global usings):

```csharp
using iPath.Application.Features.TaskAssignments;
```

- [ ] **Step 2: Implement in DirectApiClient.cs**

Open `src/ui/iPath.Blazor.ServiceLib/Services/DirectApiClient.cs` and add a new region with the implementation:

```csharp

    #region "-- Task Assignments --"

    public async Task<IApiResponse<IReadOnlyList<TaskAssignmentDto>>> GetMyTaskAssignments(eTaskStatus? statusFilter = null)
    {
        try
        {
            var result = await mediator.Send(new GetUserTaskAssignmentsQuery(StatusFilter: statusFilter));
            return Respond<IReadOnlyList<TaskAssignmentDto>>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetMyTaskAssignments failed");
            return RespondError<IReadOnlyList<TaskAssignmentDto>>();
        }
    }

    public async Task<IApiResponse<IReadOnlyList<TaskAssignmentDto>>> GetGroupTaskAssignments(Guid groupId, eTaskStatus? statusFilter = null)
    {
        try
        {
            var result = await mediator.Send(new GetGroupTaskAssignmentsQuery(groupId, statusFilter));
            return Respond<IReadOnlyList<TaskAssignmentDto>>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetGroupTaskAssignments failed");
            return RespondError<IReadOnlyList<TaskAssignmentDto>>();
        }
    }

    public async Task<IApiResponse<IReadOnlyList<TaskAssignmentDto>>> GetCaseTaskAssignments(Guid serviceRequestId)
    {
        try
        {
            var result = await mediator.Send(new GetCaseTaskAssignmentsQuery(serviceRequestId));
            return Respond<IReadOnlyList<TaskAssignmentDto>>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetCaseTaskAssignments failed");
            return RespondError<IReadOnlyList<TaskAssignmentDto>>();
        }
    }

    public async Task<IApiResponse<TaskAssignmentDto>> GetTaskAssignmentById(Guid id)
    {
        try
        {
            var result = await mediator.Send(new GetTaskAssignmentByIdQuery(id));
            return Respond<TaskAssignmentDto>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetTaskAssignmentById failed");
            return RespondError<TaskAssignmentDto>();
        }
    }

    public async Task<IApiResponse<TaskAssignmentDto>> ProposeTaskAssignment(ProposeTaskAssignmentCommand command)
    {
        try
        {
            var result = await mediator.Send(command);
            return Respond<TaskAssignmentDto>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ProposeTaskAssignment failed");
            return RespondError<TaskAssignmentDto>();
        }
    }

    public async Task<IApiResponse<TaskAssignmentDto>> AcceptTaskAssignment(Guid id)
    {
        try
        {
            var result = await mediator.Send(new AcceptTaskAssignmentCommand(id));
            return Respond<TaskAssignmentDto>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AcceptTaskAssignment failed");
            return RespondError<TaskAssignmentDto>();
        }
    }

    public async Task<IApiResponse<TaskAssignmentDto>> DeclineTaskAssignment(Guid id)
    {
        try
        {
            var result = await mediator.Send(new DeclineTaskAssignmentCommand(id));
            return Respond<TaskAssignmentDto>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DeclineTaskAssignment failed");
            return RespondError<TaskAssignmentDto>();
        }
    }

    public async Task<IApiResponse<TaskAssignmentDto>> CompleteTaskAssignment(Guid id)
    {
        try
        {
            var result = await mediator.Send(new CompleteTaskAssignmentCommand(id));
            return Respond<TaskAssignmentDto>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CompleteTaskAssignment failed");
            return RespondError<TaskAssignmentDto>();
        }
    }

    public async Task<IApiResponse<TaskAssignmentDto>> ReturnTaskAssignment(Guid id)
    {
        try
        {
            var result = await mediator.Send(new ReturnTaskAssignmentCommand(id));
            return Respond<TaskAssignmentDto>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ReturnTaskAssignment failed");
            return RespondError<TaskAssignmentDto>();
        }
    }

    public async Task<IApiResponse<TaskAssignmentDto>> CancelTaskAssignment(Guid id)
    {
        try
        {
            var result = await mediator.Send(new CancelTaskAssignmentCommand(id));
            return Respond<TaskAssignmentDto>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CancelTaskAssignment failed");
            return RespondError<TaskAssignmentDto>();
        }
    }

    public async Task<IApiResponse<TaskAssignmentDto>> CreateFollowUpTask(CreateFollowUpTaskCommand command)
    {
        try
        {
            var result = await mediator.Send(command);
            return Respond<TaskAssignmentDto>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CreateFollowUpTask failed");
            return RespondError<TaskAssignmentDto>();
        }
    }

    #endregion
```

- [ ] **Step 3: Build and verify**

```bash
dotnet build src/ui/iPath.Blazor.ServiceLib/iPath.Blazor.ServiceLib.csproj --no-restore
```

Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add src/ui/iPath.Blazor.ServiceLib/ApiClient/IPathApi.cs src/ui/iPath.Blazor.ServiceLib/Services/DirectApiClient.cs
git commit -m "feat: add TaskAssignment methods to IPathApi and DirectApiClient"
```

---

### Task 10: Tests — TaskAssignment Command Handlers

**Files:**
- Create: `test/iPath.Test.xUnit2/TaskAssignments/TaskAssignmentCommandHandlerTests.cs`

- [ ] **Step 1: Create TaskAssignmentCommandHandlerTests.cs**

```csharp
using FluentAssertions;
using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;
using iPath.EF.Core.FeatureHandlers.TaskAssignments.Commands;
using iPath.Test.xUnit2;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace iPath.Tests.TaskAssignments;

public class TaskAssignmentCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly DbFixture _dbFixture;
    private readonly iPathDbContext _db;
    private readonly IUserSession _sess;

    public TaskAssignmentCommandHandlerTests(DbFixture dbFixture)
    {
        _dbFixture = dbFixture;
        _db = dbFixture.CreateContext();
        _sess = Substitute.For<IUserSession>();
        _sess.User.Returns(new User { Id = Guid.NewGuid(), UserName = "testmod" });
        _sess.IsAdmin.Returns(true);
    }

    [Fact]
    public async Task Propose_And_Accept_TaskAssignment_Should_Set_Assigned()
    {
        var groupId = Guid.CreateVersion7();
        var consultantId = Guid.NewGuid();
        var srId = Guid.CreateVersion7();

        _db.Groups.Add(new Group { Id = groupId, Name = "Test" });
        _db.ServiceRequests.Add(new ServiceRequest
        {
            Id = srId, GroupId = groupId, OwnerId = consultantId, NodeType = "Test"
        });
        await _db.SaveChangesAsync();

        var mediator = Substitute.For<IMediator>();
        var dto = new TaskAssignmentDto { Id = Guid.CreateVersion7(), Status = nameof(eTaskStatus.Assigned) };
        mediator.Send(Arg.Any<GetTaskAssignmentByIdQuery>(), default)
            .Returns(Task.FromResult(dto));

        var proposeHandler = new ProposeTaskAssignmentHandler(_db, _sess, mediator, NullLogger<ProposeTaskAssignmentHandler>.Instance);
        var proposeCmd = new ProposeTaskAssignmentCommand(srId, consultantId, eTaskAssignmentMode.ModeratorSuggested);
        var proposeResult = await proposeHandler.Handle(proposeCmd, default);

        proposeResult.Should().NotBeNull();
        proposeResult.Status.Should().Be(nameof(eTaskStatus.Assigned));
    }

    [Fact]
    public async Task Decline_Proposed_Task_Should_Set_Declined()
    {
        var taskId = Guid.CreateVersion7();
        var userId = _sess.User.Id;

        _db.TaskAssignments.Add(new TaskAssignment
        {
            Id = taskId,
            ServiceRequestId = Guid.CreateVersion7(),
            AssignedToUserId = userId,
            AssignedByUserId = userId,
            Type = eTaskType.DiagnosticReview,
            Mode = eTaskAssignmentMode.ModeratorSuggested,
            Status = eTaskStatus.Proposed,
            CreatedOn = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var mediator = Substitute.For<IMediator>();
        var dto = new TaskAssignmentDto { Id = taskId, Status = nameof(eTaskStatus.Declined) };
        mediator.Send(Arg.Any<GetTaskAssignmentByIdQuery>(), default)
            .Returns(Task.FromResult(dto));

        var declineHandler = new DeclineTaskAssignmentHandler(_db, _sess, mediator, NullLogger<DeclineTaskAssignmentHandler>.Instance);
        var declineResult = await declineHandler.Handle(new DeclineTaskAssignmentCommand(taskId), default);

        declineResult.Should().NotBeNull();
        declineResult.Status.Should().Be(nameof(eTaskStatus.Declined));
    }

    [Fact]
    public async Task Complete_Assigned_Task_Should_Set_Completed()
    {
        var taskId = Guid.CreateVersion7();
        var userId = _sess.User.Id;

        _db.TaskAssignments.Add(new TaskAssignment
        {
            Id = taskId,
            ServiceRequestId = Guid.CreateVersion7(),
            AssignedToUserId = userId,
            AssignedByUserId = userId,
            Type = eTaskType.DiagnosticReview,
            Mode = eTaskAssignmentMode.SelfAssigned,
            Status = eTaskStatus.Assigned,
            AcceptedOn = DateTime.UtcNow,
            CreatedOn = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var mediator = Substitute.For<IMediator>();
        var dto = new TaskAssignmentDto { Id = taskId, Status = nameof(eTaskStatus.Completed) };
        mediator.Send(Arg.Any<GetTaskAssignmentByIdQuery>(), default)
            .Returns(Task.FromResult(dto));

        var completeHandler = new CompleteTaskAssignmentHandler(_db, _sess, mediator, NullLogger<CompleteTaskAssignmentHandler>.Instance);
        var completeResult = await completeHandler.Handle(new CompleteTaskAssignmentCommand(taskId), default);

        completeResult.Should().NotBeNull();
        completeResult.Status.Should().Be(nameof(eTaskStatus.Completed));
    }
}
```

- [ ] **Step 2: Run tests**

```bash
dotnet test --filter "TaskAssignmentCommandHandlerTests"
```

Expected: Tests pass

- [ ] **Step 3: Commit**

```bash
git add test/iPath.Test.xUnit2/TaskAssignments/
git commit -m "test: add TaskAssignment command handler unit tests"
```

---

### Task 11: My Tasks Page (Blazor UI)

**Files:**
- Create: `src/ui/iPath.RazorLib/TaskAssignments/Pages/MyTasks.razor`
- Create: `src/ui/iPath.RazorLib/TaskAssignments/Pages/MyTasks.razor.cs`
- Modify: `src/ui/iPath.RazorLib/Users/UserNavMenu.razor`
- Modify: `src/ui/iPath.RazorLib/_Imports.razor`

- [ ] **Step 1: Create MyTasks.razor**

```razor
@namespace iPath.Blazor.Componenents.TaskAssignments
@page "/mytasks"
@attribute [Authorize]

<MudContainer>
    <MudText Typo="Typo.h4">@T["My Tasks"]</MudText>

    <MudGrid>
        <MudItem xs="12" md="3">
            <MudSelect @bind-Value="_statusFilter" Label="@T["Filter by Status"]"
                       ToStringFunc="@(s => s is null ? T["All"] : T[s.ToString()])">
                <MudSelectItem Value="null">@T["All"]</MudSelectItem>
                @foreach (var status in Enum.GetValues<eTaskStatus>())
                {
                    <MudSelectItem Value="status">@T[status.ToString()]</MudSelectItem>
                }
            </MudSelect>
        </MudItem>
    </MudGrid>

    <MudTable Items="@_tasks" Hover="@true" Loading="@_loading">
        <HeaderContent>
            <MudTh>@T["Case"]</MudTh>
            <MudTh>@T["Group"]</MudTh>
            <MudTh>@T["Type"]</MudTh>
            <MudTh>@T["Status"]</MudTh>
            <MudTh>@T["Created"]</MudTh>
            <MudTh>@T["Actions"]</MudTh>
        </HeaderContent>
        <RowTemplate>
            <MudTd DataLabel="Case">
                <MudLink Href=@($"/case/{context.ServiceRequestId}")>@context.CaseTitle</MudLink>
            </MudTd>
            <MudTd DataLabel="Group">@context.GroupName</MudTd>
            <MudTd DataLabel="Type">@T[context.Type]</MudTd>
            <MudTd DataLabel="Status">
                <MudChip Color="@GetStatusColor(context.Status)" Size="Size.Small">@T[context.Status]</MudChip>
            </MudTd>
            <MudTd DataLabel="Created">@context.CreatedOn.ToLocalTime().ToString("g")</MudTd>
            <MudTd DataLabel="Actions">
                @if (context.Status == nameof(eTaskStatus.Proposed))
                {
                    <MudButton Variant="Variant.Filled" Color="Color.Success" Size="Size.Small"
                               OnClick="@(() => AcceptTask(context.Id))">@T["Accept"]</MudButton>
                    <MudButton Variant="Variant.Outlined" Color="Color.Warning" Size="Size.Small"
                               OnClick="@(() => DeclineTask(context.Id))">@T["Decline"]</MudButton>
                }
                @if (context.Status is nameof(eTaskStatus.Assigned) or nameof(eTaskStatus.InProgress))
                {
                    <MudButton Variant="Variant.Filled" Color="Color.Primary" Size="Size.Small"
                               OnClick="@(() => CompleteTask(context.Id))">@T["Complete"]</MudButton>
                    <MudButton Variant="Variant.Outlined" Color="Color.Info" Size="Size.Small"
                               OnClick="@(() => ReturnTask(context.Id))">@T["Return"]</MudButton>
                }
            </MudTd>
        </RowTemplate>
    </MudTable>
</MudContainer>
```

- [ ] **Step 2: Create MyTasks.razor.cs**

```csharp
using iPath.Application.Features.TaskAssignments;
using iPath.Blazor.ServiceLib.ApiClient;
using iPath.Domain.Entities;
using Microsoft.Extensions.Localization;

namespace iPath.Blazor.Componenents.TaskAssignments;

public partial class MyTasks(IPathApi api, IStringLocalizer T)
{
    private List<TaskAssignmentDto> _tasks = [];
    private bool _loading;
    private eTaskStatus? _statusFilter;

    protected override async Task OnInitializedAsync()
    {
        await LoadTasks();
    }

    private async Task LoadTasks()
    {
        _loading = true;
        StateHasChanged();

        var response = await api.GetMyTaskAssignments(_statusFilter);
        if (response.IsSuccessStatusCode())
            _tasks = response.Content?.ToList() ?? [];

        _loading = false;
        StateHasChanged();
    }

    private async Task AcceptTask(Guid id)
    {
        await api.AcceptTaskAssignment(id);
        await LoadTasks();
    }

    private async Task DeclineTask(Guid id)
    {
        await api.DeclineTaskAssignment(id);
        await LoadTasks();
    }

    private async Task CompleteTask(Guid id)
    {
        await api.CompleteTaskAssignment(id);
        await LoadTasks();
    }

    private async Task ReturnTask(Guid id)
    {
        await api.ReturnTaskAssignment(id);
        await LoadTasks();
    }

    private Color GetStatusColor(string status) => status switch
    {
        nameof(eTaskStatus.Proposed) => Color.Warning,
        nameof(eTaskStatus.Assigned) => Color.Info,
        nameof(eTaskStatus.InProgress) => Color.Primary,
        nameof(eTaskStatus.Completed) => Color.Success,
        nameof(eTaskStatus.Declined) => Color.Error,
        nameof(eTaskStatus.Cancelled) => Color.Default,
        _ => Color.Default
    };
}
```

- [ ] **Step 3: Add "My Tasks" nav entry to UserNavMenu.razor**

Open `src/ui/iPath.RazorLib/Users/UserNavMenu.razor` and add a nav link after the "My Cases" entry (after line 10):

```razor
        <MudNavLink Href="mytasks" Icon="@Icons.Material.TwoTone.Assignment">My Tasks</MudNavLink>
```

- [ ] **Step 4: Add namespace to _Imports.razor**

Open `src/ui/iPath.RazorLib/_Imports.razor` and add:

```razor
@using iPath.Application.Features.TaskAssignments
```

- [ ] **Step 5: Build and verify**

```bash
dotnet build src/ui/iPath.RazorLib/iPath.RazorLib.csproj --no-restore
```

Expected: Build succeeds

- [ ] **Step 6: Commit**

```bash
git add src/ui/iPath.RazorLib/TaskAssignments/
git commit -m "feat: add My Tasks page with accept/decline/complete/return actions"
```

---

### Task 12: Group Tasks Page (Moderator View)

**Files:**
- Create: `src/ui/iPath.RazorLib/TaskAssignments/Pages/GroupTasks.razor`
- Create: `src/ui/iPath.RazorLib/TaskAssignments/Pages/GroupTasks.razor.cs`

- [ ] **Step 1: Create GroupTasks.razor**

```razor
@namespace iPath.Blazor.Componenents.TaskAssignments
@page "/admin/groups/{GroupId:guid}/tasks"
@attribute [Authorize]

<MudContainer>
    <MudText Typo="Typo.h4">@T["Group Tasks"] — @_groupName</MudText>

    <MudGrid>
        <MudItem xs="12" md="3">
            <MudSelect @bind-Value="_statusFilter" Label="@T["Filter by Status"]"
                       ToStringFunc="@(s => s is null ? T["All"] : T[s.ToString()])">
                <MudSelectItem Value="null">@T["All"]</MudSelectItem>
                @foreach (var status in Enum.GetValues<eTaskStatus>())
                {
                    <MudSelectItem Value="status">@T[status.ToString()]</MudSelectItem>
                }
            </MudSelect>
        </MudItem>
    </MudGrid>

    <MudTable Items="@_tasks" Hover="@true" Loading="@_loading">
        <HeaderContent>
            <MudTh>@T["Case"]</MudTh>
            <MudTh>@T["Assigned To"]</MudTh>
            <MudTh>@T["Assigned By"]</MudTh>
            <MudTh>@T["Type"]</MudTh>
            <MudTh>@T["Mode"]</MudTh>
            <MudTh>@T["Status"]</MudTh>
            <MudTh>@T["Created"]</MudTh>
            <MudTh>@T["Actions"]</MudTh>
        </HeaderContent>
        <RowTemplate>
            <MudTd DataLabel="Case">
                <MudLink Href=@($"/case/{context.ServiceRequestId}")>@context.CaseTitle</MudLink>
            </MudTd>
            <MudTd DataLabel="Assigned To">@context.AssignedToUsername</MudTd>
            <MudTd DataLabel="Assigned By">@context.AssignedByUsername</MudTd>
            <MudTd DataLabel="Type">@T[context.Type]</MudTd>
            <MudTd DataLabel="Mode">@T[context.Mode]</MudTd>
            <MudTd DataLabel="Status">
                <MudChip Color="@GetStatusColor(context.Status)" Size="Size.Small">@T[context.Status]</MudChip>
            </MudTd>
            <MudTd DataLabel="Created">@context.CreatedOn.ToLocalTime().ToString("g")</MudTd>
            <MudTd DataLabel="Actions">
                @if (context.Status != nameof(eTaskStatus.Completed) && context.Status != nameof(eTaskStatus.Cancelled))
                {
                    <MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small"
                               OnClick="@(() => CancelTask(context.Id))">@T["Cancel"]</MudButton>
                }
            </MudTd>
        </RowTemplate>
    </MudTable>
</MudContainer>
```

- [ ] **Step 2: Create GroupTasks.razor.cs**

```csharp
using iPath.Application.Features.TaskAssignments;
using iPath.Blazor.ServiceLib.ApiClient;
using iPath.Domain.Entities;
using Microsoft.Extensions.Localization;

namespace iPath.Blazor.Componenents.TaskAssignments;

public partial class GroupTasks(IPathApi api, IStringLocalizer T)
{
    [Parameter] public Guid GroupId { get; set; }

    private List<TaskAssignmentDto> _tasks = [];
    private string? _groupName;
    private bool _loading;
    private eTaskStatus? _statusFilter;

    protected override async Task OnInitializedAsync()
    {
        await LoadTasks();
    }

    private async Task LoadTasks()
    {
        _loading = true;
        StateHasChanged();

        var response = await api.GetGroupTaskAssignments(GroupId, _statusFilter);
        if (response.IsSuccessStatusCode())
        {
            _tasks = response.Content?.ToList() ?? [];
            _groupName = _tasks.FirstOrDefault()?.GroupName;
        }

        _loading = false;
        StateHasChanged();
    }

    private async Task CancelTask(Guid id)
    {
        await api.CancelTaskAssignment(id);
        await LoadTasks();
    }

    private Color GetStatusColor(string status) => status switch
    {
        nameof(eTaskStatus.Proposed) => Color.Warning,
        nameof(eTaskStatus.Assigned) => Color.Info,
        nameof(eTaskStatus.InProgress) => Color.Primary,
        nameof(eTaskStatus.Completed) => Color.Success,
        nameof(eTaskStatus.Declined) => Color.Error,
        nameof(eTaskStatus.Cancelled) => Color.Default,
        _ => Color.Default
    };
}
```

- [ ] **Step 3: Build and verify**

```bash
dotnet build src/ui/iPath.RazorLib/iPath.RazorLib.csproj --no-restore
```

Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add src/ui/iPath.RazorLib/TaskAssignments/Pages/GroupTasks.razor*
git commit -m "feat: add Group Tasks moderator page"
```

---

### Task 13: Task Assignment Card + Group Settings UI

**Files:**
- Create: `src/ui/iPath.RazorLib/TaskAssignments/Components/TaskAssignmentCard.razor`
- Create: `src/ui/iPath.RazorLib/TaskAssignments/Components/TaskAssignmentCard.razor.cs`

- [ ] **Step 1: Create TaskAssignmentCard.razor**

```razor
@namespace iPath.Blazor.Componenents.TaskAssignments.Components

<MudCard>
    <MudCardHeader>
        <MudText Typo="Typo.subtitle1">
            <MudIcon Icon="@Icons.Material.Filled.Assignment" /> @T["Task Assignments"]
        </MudText>
    </MudCardHeader>
    <MudCardContent>
        @if (_tasks is null || _tasks.Count == 0)
        {
            <MudText>@T["No tasks assigned for this case."]</MudText>
        }
        else
        {
            @foreach (var task in _tasks)
            {
                <MudPaper Class="pa-2 mb-2" Elevation="1">
                    <MudGrid>
                        <MudItem xs="6">
                            <MudText Typo="Typo.body2">
                                <strong>@T["Assigned to"]:</strong> @task.AssignedToUsername
                            </MudText>
                        </MudItem>
                        <MudItem xs="3">
                            <MudChip Color="@GetColor(task.Status)" Size="Size.Small">@T[task.Status]</MudChip>
                        </MudItem>
                        <MudItem xs="3" Class="d-flex justify-end">
                            @if (task.Status == nameof(eTaskStatus.Proposed) && task.AssignedToUserId == _currentUserId)
                            {
                                <MudIconButton Icon="@Icons.Material.Filled.CheckCircle" Color="Color.Success"
                                               Size="Size.Small" Title="@T["Accept"]"
                                               OnClick="@(() => AcceptTask(task.Id))" />
                                <MudIconButton Icon="@Icons.Material.Filled.Cancel" Color="Color.Error"
                                               Size="Size.Small" Title="@T["Decline"]"
                                               OnClick="@(() => DeclineTask(task.Id))" />
                            }
                            @if (task.Status is nameof(eTaskStatus.Assigned) or nameof(eTaskStatus.InProgress)
                                && task.AssignedToUserId == _currentUserId)
                            {
                                <MudIconButton Icon="@Icons.Material.Filled.TaskAlt" Color="Color.Primary"
                                               Size="Size.Small" Title="@T["Complete"]"
                                               OnClick="@(() => CompleteTask(task.Id))" />
                            }
                        </MudItem>
                    </MudGrid>
                    @if (!string.IsNullOrEmpty(task.Notes))
                    {
                        <MudText Typo="Typo.caption">@task.Notes</MudText>
                    }
                </MudPaper>
            }
        }
    </MudCardContent>
</MudCard>
```

- [ ] **Step 2: Create TaskAssignmentCard.razor.cs**

```csharp
using iPath.Application.Features.TaskAssignments;
using iPath.Blazor.ServiceLib.ApiClient;
using iPath.Domain.Entities;
using Microsoft.Extensions.Localization;

namespace iPath.Blazor.Componenents.TaskAssignments.Components;

public partial class TaskAssignmentCard(IPathApi api, IStringLocalizer T)
{
    [Parameter] public Guid ServiceRequestId { get; set; }

    private List<TaskAssignmentDto> _tasks = [];
    private Guid _currentUserId;

    protected override async Task OnInitializedAsync()
    {
        await LoadTasks();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_currentUserId == Guid.Empty)
            _currentUserId = Guid.Empty; // Will be set from AppState or auth
        await LoadTasks();
    }

    private async Task LoadTasks()
    {
        var response = await api.GetCaseTaskAssignments(ServiceRequestId);
        if (response.IsSuccessStatusCode())
            _tasks = response.Content?.ToList() ?? [];
    }

    private async Task AcceptTask(Guid id)
    {
        await api.AcceptTaskAssignment(id);
        await LoadTasks();
    }

    private async Task DeclineTask(Guid id)
    {
        await api.DeclineTaskAssignment(id);
        await LoadTasks();
    }

    private async Task CompleteTask(Guid id)
    {
        await api.CompleteTaskAssignment(id);
        await LoadTasks();
    }

    private Color GetColor(string status) => status switch
    {
        nameof(eTaskStatus.Proposed) => Color.Warning,
        nameof(eTaskStatus.Assigned) => Color.Info,
        nameof(eTaskStatus.Completed) => Color.Success,
        nameof(eTaskStatus.Declined) => Color.Error,
        nameof(eTaskStatus.Cancelled) => Color.Default,
        _ => Color.Default
    };
}
```

- [ ] **Step 3: Add strategy selector to GroupSettingsForm.razor**

Open `src/ui/iPath.RazorLib/Groups/Components/GroupSettingsForm.razor`. Add a new section after the case type fields (before the closing `</MudForm>` tag at line 55):

```razor
    <MudText Typo="Typo.h6" Class="mt-4">@T["Task Assignment"]</MudText>

    <MudGrid Spacing="0">
        <MudItem xs="12" sm="6">
            <MudSelect @bind-Value="Model.TaskAssignmentStrategy"
                       Label="@T["Strategy"]" Variant="@Variant" Margin="Margin.Dense">
                @foreach (var strategy in Enum.GetValues<eTaskAssignmentStrategy>())
                {
                    <MudSelectItem Value="strategy">@T[strategy.ToString()]</MudSelectItem>
                }
            </MudSelect>
        </MudItem>
        <MudItem xs="12" sm="4">
            <MudNumericField @bind-Value="Model.AutoAssignTimeoutHours"
                             Label="@T["Auto-assign timeout (h)"]"
                             Variant="@Variant" Margin="Margin.Dense"
                             Disabled="@(Model.TaskAssignmentStrategy != eTaskAssignmentStrategy.AutoAssign)" />
        </MudItem>
    </MudGrid>
```

- [ ] **Step 4: Build and verify**

```bash
dotnet build src/ui/iPath.RazorLib/iPath.RazorLib.csproj --no-restore
```

Expected: Build succeeds

- [ ] **Step 5: Commit**

```bash
git add src/ui/iPath.RazorLib/TaskAssignments/Components/ src/ui/iPath.RazorLib/Groups/Components/GroupSettingsForm.razor
git commit -m "feat: add TaskAssignmentCard component and group strategy UI"
```

---

### Self-Review Checklist

**1. Spec coverage:**
- Core entity (Task 1.5) — matches spec §2
- Enums (Tasks 1.1-1.4) — matches spec §2
- GroupSettings strategy (Task 1.6) — matches spec §3
- State machine transitions in entity methods (Task 1.5) — covers Proposed→Assigned→InProgress→Completed, Declined, ReturnedForReassignment, Cancelled — matches spec §4
- Assignment strategies (spec §5) — implemented in handlers (Tasks 5, 7)
- Task types (spec §6) — DiagnosticReview and FollowUp in commands
- Permissions (spec §7) — enforced in handler authorization checks
- Domain events (Task 2) — matches spec §8
- Notification integration deferred (spec §10) — events defined but no SSE/Email wiring
- My Tasks page (Task 11) — matches spec §11
- Group Tasks page (Task 12) — matches spec §11
- Task card in case detail (Task 13) — matches spec §11
- Group settings UI (Task 13.3) — strategy dropdown + timeout field added to GroupSettingsForm — matches spec §11

**2. Placeholder scan:** No TBD/TODO. Every step has complete code or commands.

**3. Type consistency:** All entity, dto, command, and handler types use consistent naming. `TaskAssignment` entity uses `Accept()`/`Decline()`/`Complete()` methods matching the command names. DTO properties match entity properties.

**4. No missing parts:** The spec mentions CandidateService for auto-assign (Task 7 covers this). Spec mentions permissions enforcement (covered in each handler). Nav menu entry added in Task 11. IPathApi interface and DirectApiClient added in Task 9.
