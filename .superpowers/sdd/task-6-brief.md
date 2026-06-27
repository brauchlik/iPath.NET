### Task 6: `IPathApi` Refit methods + `DirectApiClient` implementations

**Files:**
- Modify: `src/ui/iPath.Blazor.ServiceLib/ApiClient/IApiClient.cs` — add 4 Refit methods
- Modify: `src/ui/iPath.Blazor.ServiceLib/Services/DirectApiClient.cs` — implement the 4 methods
- Test: `test/iPath.Test.xUnit2/CaseRoom/DirectApiClientCaseRoomTests.cs`

**Interfaces:**
- Consumes: `ICaseRoomSessionStore` (Task 3) for `DirectApiClient`, `CaseRoomModels` (Task 1)
- Produces: `IPathApi.JoinCaseRoomAsync`, `LeaveCaseRoomAsync`, `SyncCaseRoomAsync`, `GetCaseRoomStatusAsync`

- [ ] **Step 1: Add Refit methods to `IPathApi`**

Modify `src/ui/iPath.Blazor.ServiceLib/ApiClient/IApiClient.cs`. Add `using iPath.Application.Features.CaseRoom;` at the top, and add a new region after the existing `-- Notifications --` region (or wherever geographically sensible adjacent to ServiceRequest region):

```csharp
    #region "-- CaseRoom --"
    [Post("/api/v1/caseroom/{requestId}/join")]
    Task<IApiResponse<CaseRoomSnapshot>> JoinCaseRoom(Guid requestId);

    [Post("/api/v1/caseroom/{requestId}/leave")]
    Task<IApiResponse> LeaveCaseRoom(Guid requestId);

    [Post("/api/v1/caseroom/{requestId}/sync")]
    Task<IApiResponse> SyncCaseRoom(Guid requestId, [Body] SyncPayload payload);

    [Get("/api/v1/caseroom/{requestId}")]
    Task<IApiResponse<CaseRoomStatus?>> GetCaseRoomStatus(Guid requestId);
    #endregion
```

- [ ] **Step 2: Implement on DirectApiClient**

Modify `src/ui/iPath.Blazor.ServiceLib/Services/DirectApiClient.cs`:

1. Add `using iPath.API.Services.CaseRoom;` and `using iPath.Application.Features.CaseRoom;` at the top.
2. Add `ICaseRoomSessionStore caseRoomStore` as a constructor parameter (the last optional one — pattern matches the existing `syncRunner`, `jobManager`, `queue` parameters).

Construct signature change:

```csharp
public class DirectApiClient(
    IMediator mediator,
    IGroupService groupService,
    IEmailRepository emailRepo,
    INotificationRepository notificationRepo,
    IUserSession userSession,
    ILocalizationDataProvider localization,
    IOptions<iPathClientConfig> config,
    ILogger<DirectApiClient> logger,
    ISyncImportRunner? syncRunner = null,
    ISyncJobManager? jobManager = null,
    IAiExtractionQueue? queue = null,
    ICaseRoomSessionStore? caseRoomStore = null)
    : IPathApi
```

3. Implement the 4 methods at the end of the class (last `#endregion`):

```csharp
    // -- CaseRoom --

    public async Task<IApiResponse<CaseRoomSnapshot>> JoinCaseRoom(Guid requestId)
    {
        if (caseRoomStore is null || userSession.User is null)
            return RespondError<CaseRoomSnapshot>();
        var snap = await caseRoomStore.JoinAsync(requestId, userSession.User.Id, userSession.User.DisplayName ?? "Anonymous", default);
        return Respond(snap);
    }

    public async Task<IApiResponse> LeaveCaseRoom(Guid requestId)
    {
        if (caseRoomStore is null || userSession.User is null)
            return RespondError();
        await caseRoomStore.LeaveAsync(requestId, userSession.User.Id, default);
        return RespondOk();
    }

    public async Task<IApiResponse> SyncCaseRoom(Guid requestId, SyncPayload payload)
    {
        if (caseRoomStore is null || userSession.User is null)
            return RespondError();
        await caseRoomStore.SyncAsync(requestId, userSession.User.Id, payload, default);
        return RespondOk();
    }

    public async Task<IApiResponse<CaseRoomStatus?>> GetCaseRoomStatus(Guid requestId)
    {
        if (caseRoomStore is null)
            return Respond<CaseRoomStatus?>(null);
        var status = await caseRoomStore.GetStatusAsync(requestId, default);
        return Respond(status);
    }
```

> **Note:** The `DirectApiClient` lives in iPath.Blazor.ServiceLib. It references `iPath.API` only because `caseRoomStore` lives there — verify `iPath.API.csproj` is referenced by `iPath.Blazor.ServiceLib.csproj`. It already is: `DirectApiClient` imports `iPath.API.Services` indirectly via other handlers. If the using `iPath.API.Services.CaseRoom` causes a build error (missing project reference), add a project reference to `iPath.API.csproj` from `iPath.Blazor.ServiceLib.csproj`.

- [ ] **Step 3: Write a smoke test for DirectApiClient CaseRoom methods**

Create `test/iPath.Test.xUnit2/CaseRoom/DirectApiClientCaseRoomTests.cs`:

```csharp
using iPath.API.Services.CaseRoom;
using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Notifications;
using iPath.Blazor.ServiceLib.Services;
using iPath.Application.Contracts;
using iPath.Application.Localization;
using iPath.Domain.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using DispatchR;
using iPath.Application.Contracts;
using FluentAssertions;

namespace iPath.Test.xUnit2.CaseRoom;

public class DirectApiClientCaseRoomTests
{
    private static (DirectApiClient client, CaseRoomSessionStore store) CreateClient()
    {
        var store = new CaseRoomSessionStore(
            Substitute.For<iPath.API.Services.Notifications.ISseConnectionManager>(),
            new NotificationEventBus(),
            new LoggerFactory().CreateLogger<CaseRoomSessionStore>());

        var mediator = Substitute.For<IMediator>();
        var userSession = Substitute.For<IUserSession>();
        userSession.User.Returns(new SessionUserDto { Id = Guid.NewGuid(), DisplayName = "Test", IsAuthenticated = true });

        var opts = Substitute.For<IOptions<iPathClientConfig>>();
        opts.Value.Returns(new iPathClientConfig());

        var client = new DirectApiClient(
            mediator: mediator,
            groupService: Substitute.For<IGroupService>(),
            emailRepo: Substitute.For<IEmailRepository>(),
            notificationRepo: Substitute.For<INotificationRepository>(),
            userSession: userSession,
            localization: Substitute.For<ILocalizationDataProvider>(),
            config: opts,
            logger: new LoggerFactory().CreateLogger<DirectApiClient>(),
            caseRoomStore: store);

        return (client, store);
    }

    [Fact]
    public async Task DirectApiClient_JoinCaseRoom_ReturnsSnapshotFromStore()
    {
        var (client, store) = CreateClient();
        var requestId = Guid.NewGuid();

        var resp = await client.JoinCaseRoom(requestId);

        resp.IsSuccessful.Should().BeTrue();
        resp.Content!.RequestId.Should().Be(requestId);
        resp.Content.Participants.Should().ContainSingle();
    }

    [Fact]
    public async Task DirectApiClient_SyncCaseRoom_PersistsViewport()
    {
        var (client, store) = CreateClient();
        var requestId = Guid.NewGuid();
        await client.JoinCaseRoom(requestId);

        await client.SyncCaseRoom(requestId, new SyncPayload(null, new ViewportState(0.1, 0.2, 0.3)));

        var status = await client.GetCaseRoomStatus(requestId);
        status.Content!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DirectApiClient_GetCaseRoomStatus_ReturnsNullWhenNoSession()
    {
        var (client, _) = CreateClient();
        var resp = await client.GetCaseRoomStatus(Guid.NewGuid());
        resp.Content.Should().BeNull();
    }
}
```

> **Note:** Property signings on `SessionUserDto` (e.g., `DisplayName`) must match the existing `SessionUserDto` definition in `iPath.Application.Contracts`. If `SessionUserDto` doesn't have `DisplayName`, fall back to whatever property exists — verify by `grep`-ing for `public.*SessionUserDto` and reading the file. The Refit-side WASM client doesn't need this; only `DirectApiClient`'s call site here uses it.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj --filter "FullyQualifiedName~DirectApiClientCaseRoomTests"`
Expected: PASS. If project-reference issue arises for `iPath.API.Services.CaseRoom`, fix the csproj references first.

- [ ] **Step 5: Commit**

```bash
git add src/ui/iPath.Blazor.ServiceLib/ test/iPath.Test.xUnit2/CaseRoom/DirectApiClientCaseRoomTests.cs
git commit -m "feat(caseroom): add IPathApi Refit methods and DirectApiClient implementations"
```

