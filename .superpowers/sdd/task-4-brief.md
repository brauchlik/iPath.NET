### Task 4: CaseRoom API endpoints

**Files:**
- Create: `src/infrastructure/iPath.API/Endpoints/CaseRoomEndpoints.cs`
- Modify: `src/infrastructure/iPath.API/MapEndpoints.cs:36` — add `.MapCaseRoomApi()`
- Modify: `src/infrastructure/iPath.API/APIServicesRegistration.cs:113` — register `ICaseRoomSessionStore` as singleton

**Interfaces:**
- Consumes: `ICaseRoomSessionStore` (Task 3), `IUserSession` (existing)
- Produces: 4 endpoints under `/api/v1/caseroom/{requestId}:...`

- [ ] **Step 1: Register the session store as singleton**

Modify `src/infrastructure/iPath.API/APIServicesRegistration.cs`. After line 116 (`services.AddSingleton<INotificationEventBus, NotificationEventBus>();`), add:

```csharp
        // CaseRoom session store (in-memory, transient sessions)
        services.AddSingleton<ICaseRoomSessionStore, CaseRoomSessionStore>();
```

And add at the top with the other using directives (preserve alphabetical-style ordering):

```csharp
using iPath.API.Services.CaseRoom;
```

- [ ] **Step 2: Create the endpoints file**

Create `src/infrastructure/iPath.API/Endpoints/CaseRoomEndpoints.cs`:

```csharp
using iPath.API.Services.CaseRoom;
using iPath.Application.Features.CaseRoom;

namespace iPath.API;

public static class CaseRoomEndpoints
{
    public static IEndpointRouteBuilder MapCaseRoomApi(this IEndpointRouteBuilder route)
    {
        var group = route.MapGroup("caseroom").RequireAuthorization();

        group.MapPost("{requestId:guid}/join", async (
            Guid requestId,
            [FromServices] ICaseRoomSessionStore store,
            [FromServices] IUserSession sess,
            CancellationToken ct) =>
        {
            if (sess.User is null || !sess.User.IsAuthenticated)
                return Results.Unauthorized();

            var snapshot = await store.JoinAsync(requestId, sess.User.Id, sess.User.DisplayName ?? "Anonymous", ct);
            return Results.Ok(snapshot);
        });

        group.MapPost("{requestId:guid}/leave", async (
            Guid requestId,
            [FromServices] ICaseRoomSessionStore store,
            [FromServices] IUserSession sess,
            CancellationToken ct) =>
        {
            if (sess.User is null || !sess.User.IsAuthenticated)
                return Results.Unauthorized();

            await store.LeaveAsync(requestId, sess.User.Id, ct);
            return Results.NoContent();
        });

        group.MapPost("{requestId:guid}/sync", async (
            Guid requestId,
            [FromServices] ICaseRoomSessionStore store,
            [FromServices] IUserSession sess,
            SyncPayload payload,
            CancellationToken ct) =>
        {
            if (sess.User is null || !sess.User.IsAuthenticated)
                return Results.Unauthorized();

            await store.SyncAsync(requestId, sess.User.Id, payload, ct);
            return Results.NoContent();
        });

        group.MapGet("{requestId:guid}", async (
            Guid requestId,
            [FromServices] ICaseRoomSessionStore store,
            CancellationToken ct) =>
        {
            var status = await store.GetStatusAsync(requestId, ct);
            return status is null ? Results.NotFound() : Results.Ok(status);
        });

        return route;
    }
}
```

> **Note:** `IUserSession.User` exposes `Id`, `IsAuthenticated`, and `DisplayName`. Verify these property names by reading `src/infrastructure/iPath.API/Services/UserSession.cs` or the `IUserSession` interface if any naming mismatch arises — the existing `NotificationEndpoints.cs` uses `sess.User.Id` and `sess.User.IsAuthenticated`, follow that pattern exactly.

- [ ] **Step 3: Wire up MapCaseRoomApi in the endpoint chain**

Modify `src/infrastructure/iPath.API/MapEndpoints.cs:36`. Change the chain from:

```csharp
            .MapTaskAssignmentEndpoints()
            .MapSyncApi();
```

to:

```csharp
            .MapTaskAssignmentEndpoints()
            .MapSyncApi()
            .MapCaseRoomApi();
```

- [ ] **Step 4: Build to verify everything compiles**

Run: `dotnet build src/infrastructure/iPath.API/iPath.API.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/infrastructure/iPath.API/Endpoints/CaseRoomEndpoints.cs src/infrastructure/iPath.API/MapEndpoints.cs src/infrastructure/iPath.API/APIServicesRegistration.cs
git commit -m "feat(caseroom): add API endpoints for join/leave/sync/status"
```

