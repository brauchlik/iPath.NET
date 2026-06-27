### Task 1: Domain models and sync contracts

**Files:**
- Create: `src/core/iPath.Application/Features/CaseRoom/CaseRoomModels.cs`
- Create: `src/core/iPath.Application/Features/CaseRoom/ICaseRoomSyncService.cs`
- Test: `test/iPath.Test.xUnit2/CaseRoom/CaseRoomModelsTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces:
  - `ViewportState(double X, double Y, double Zoom)`
  - `Participant(Guid UserId, string DisplayName, DateTimeOffset JoinedAt)`
  - `SyncPayload(Guid? DocumentId, ViewportState? Viewport)`
  - `CaseRoomSnapshot(Guid RequestId, Guid? ActiveDocumentId, ViewportState? Viewport, Participant[] Participants)`
  - `CaseRoomStatus(bool IsActive, int ParticipantCount, string[] ParticipantNames)`
  - `CaseRoomSyncEvent(Guid RequestId, Guid UserId, string DisplayName, SyncPayload Payload, DateTimeOffset Timestamp)`
  - `ICaseRoomSyncService` with `JoinAsync/LeaveAsync/SyncAsync` returning `Task`/`Task<CaseRoomSnapshot>`
  - `ICaseRoomSyncReceiver` with `IDisposable Subscribe(Guid requestId, Action<CaseRoomSyncEvent> handler)`

- [ ] **Step 1: Write the failing test**

Create `test/iPath.Test.xUnit2/CaseRoom/CaseRoomModelsTests.cs`:

```csharp
using iPath.Application.Features.CaseRoom;
using FluentAssertions;

namespace iPath.Test.xUnit2.CaseRoom;

public class CaseRoomModelsTests
{
    [Fact]
    public void ViewportState_ConstructsWithXYZ()
    {
        var v = new ViewportState(1.5, 2.5, 3.5);
        v.X.Should().Be(1.5);
        v.Y.Should().Be(2.5);
        v.Zoom.Should().Be(3.5);
    }

    [Fact]
    public void SyncPayload_AllowsDocumentOnly()
    {
        var p = new SyncPayload(DocumentId: Guid.NewGuid(), Viewport: null);
        p.DocumentId.Should().NotBeNull();
        p.Viewport.Should().BeNull();
    }

    [Fact]
    public void SyncPayload_AllowsViewportOnly()
    {
        var p = new SyncPayload(DocumentId: null, Viewport: new ViewportState(1, 2, 3));
        p.DocumentId.Should().BeNull();
        p.Viewport.Should().NotBeNull();
    }

    [Fact]
    public void CaseRoomSyncEvent_HasRequestIdUserIdAndPayload()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var payload = new SyncPayload(null, new ViewportState(0.5, 0.5, 1.0));
        var evt = new CaseRoomSyncEvent(requestId, userId, "Alice", payload, DateTimeOffset.UtcNow);

        evt.RequestId.Should().Be(requestId);
        evt.UserId.Should().Be(userId);
        evt.DisplayName.Should().Be("Alice");
        evt.Payload.Viewport!.Zoom.Should().Be(1.0);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj --filter "FullyQualifiedName~CaseRoomModelsTests"`
Expected: Build failure — namespace `iPath.Application.Features.CaseRoom` does not exist.

- [ ] **Step 3: Create the models file**

Create `src/core/iPath.Application/Features/CaseRoom/CaseRoomModels.cs`:

```csharp
namespace iPath.Application.Features.CaseRoom;

public record ViewportState(double X, double Y, double Zoom);

public record Participant(Guid UserId, string DisplayName, DateTimeOffset JoinedAt);

public record SyncPayload(Guid? DocumentId, ViewportState? Viewport);

public record CaseRoomSnapshot(
    Guid RequestId,
    Guid? ActiveDocumentId,
    ViewportState? Viewport,
    Participant[] Participants);

public record CaseRoomStatus(bool IsActive, int ParticipantCount, string[] ParticipantNames);

public record CaseRoomSyncEvent(
    Guid RequestId,
    Guid UserId,
    string DisplayName,
    SyncPayload Payload,
    DateTimeOffset Timestamp);
```

- [ ] **Step 4: Create the sync service interfaces**

Create `src/core/iPath.Application/Features/CaseRoom/ICaseRoomSyncService.cs`:

```csharp
namespace iPath.Application.Features.CaseRoom;

public interface ICaseRoomSyncService : IAsyncDisposable
{
    Task<CaseRoomSnapshot> JoinAsync(Guid requestId, CancellationToken ct = default);
    Task LeaveAsync(Guid requestId, CancellationToken ct = default);
    Task SyncAsync(Guid requestId, SyncPayload payload, CancellationToken ct = default);
}

public interface ICaseRoomSyncReceiver
{
    IDisposable Subscribe(Guid requestId, Action<CaseRoomSyncEvent> handler);
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj --filter "FullyQualifiedName~CaseRoomModelsTests"`
Expected: PASS (all 4 tests).

- [ ] **Step 6: Commit**

```bash
git add src/core/iPath.Application/Features/CaseRoom/ test/iPath.Test.xUnit2/CaseRoom/
git commit -m "feat(caseroom): add domain models and sync service contracts"
```

