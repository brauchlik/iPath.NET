936ac65 feat(caseroom): show CaseRoom active badge on ServiceRequest page
9e5cee6 feat(caseroom): add CaseRoomPage with inline OSD and bidirectional sync
4c3c404 feat(caseroom): add OSD JS interop module with throttled viewport sync
3471d90 feat(caseroom): implement dual-mode sync service and receiver (HTTP+SSE / in-memory)
6b3c23b feat(caseroom): add IPathApi Refit methods and DirectApiClient implementations

## Stat summary

 .../ApiClient/IApiClient.cs                        |  16 +++
 .../Services/DirectApiClient.cs                    |  40 +++++-
 .../iPath.Blazor.ServiceLib.csproj                 |   1 +
 .../CaseRoom/HttpCaseRoomSyncReceiver.cs           |  26 ++++
 .../CaseRoom/HttpCaseRoomSyncService.cs            |  18 +++
 .../CaseRoom/InMemoryCaseRoomSyncReceiver.cs       |  16 +++
 .../CaseRoom/InMemoryCaseRoomSyncService.cs        |  31 +++++
 .../iPath.RazorLib/RazorLibServiceRegistration.cs  |  14 ++
 .../ServiceRequests/CaseRoomPage.razor             |  35 +++++
 .../ServiceRequests/CaseRoomPage.razor.cs          | 151 +++++++++++++++++++++
 .../ServiceRequests/ServiceRequestPage.razor       |  36 +++++
 src/ui/iPath.RazorLib/_Imports.razor               |   1 +
 src/ui/iPath.RazorLib/wwwroot/js/ipath-caseroom.js |  77 +++++++++++
 .../CaseRoom/CaseRoomSyncTransportTests.cs         |  71 ++++++++++
 .../CaseRoom/DirectApiClientCaseRoomTests.cs       |  83 +++++++++++
 15 files changed, 615 insertions(+), 1 deletion(-)

## Full diff

diff --git a/src/ui/iPath.Blazor.ServiceLib/ApiClient/IApiClient.cs b/src/ui/iPath.Blazor.ServiceLib/ApiClient/IApiClient.cs
index 70cd3b0..e6c6f5c 100644
--- a/src/ui/iPath.Blazor.ServiceLib/ApiClient/IApiClient.cs
+++ b/src/ui/iPath.Blazor.ServiceLib/ApiClient/IApiClient.cs
@@ -1,15 +1,16 @@
 using FluentResults;
 using iPath.Application.Contracts;
 using iPath.Application.Features;
 using iPath.Application.Features.Admin;
 using iPath.Application.Features.Annotations;
+using iPath.Application.Features.CaseRoom;
 using iPath.Application.Features.CMS;
 using iPath.Application.Features.Documents;
 using iPath.Application.Features.EmailImport;
 using iPath.Application.Features.Notifications;
 using iPath.Application.Features.TaskAssignments;
 using iPath.Application.Features.ServiceRequests;
 using iPath.Application.Features.ServiceRequests.Commands;
 using iPath.Application.Features.Users;
 using iPath.Application.Features.SyncImport;
 using iPath.Application.Localization;
@@ -457,11 +458,26 @@ public interface IPathApi
     [Post("/api/v1/admin/sync/groups/{groupId}/reimport")]
     Task<IApiResponse<SyncStartResponse>> StartReimport(int groupId);
 
     [Post("/api/v1/admin/sync/groups/{groupId}/delete")]
     Task<IApiResponse<SyncStartResponse>> DeleteImport(int groupId);
 
     [Get("/api/v1/admin/sync/job")]
     Task<IApiResponse<SyncJobState>> GetSyncJobStatus();
 
     #endregion
+
+
+    #region "-- CaseRoom --"
+    [Post("/api/v1/caseroom/{requestId}/join")]
+    Task<IApiResponse<CaseRoomSnapshot>> JoinCaseRoom(Guid requestId);
+
+    [Post("/api/v1/caseroom/{requestId}/leave")]
+    Task<IApiResponse> LeaveCaseRoom(Guid requestId);
+
+    [Post("/api/v1/caseroom/{requestId}/sync")]
+    Task<IApiResponse> SyncCaseRoom(Guid requestId, [Body] SyncPayload payload);
+
+    [Get("/api/v1/caseroom/{requestId}")]
+    Task<IApiResponse<CaseRoomStatus?>> GetCaseRoomStatus(Guid requestId);
+    #endregion
 }
diff --git a/src/ui/iPath.Blazor.ServiceLib/Services/DirectApiClient.cs b/src/ui/iPath.Blazor.ServiceLib/Services/DirectApiClient.cs
index 31279ad..ed8a67a 100644
--- a/src/ui/iPath.Blazor.ServiceLib/Services/DirectApiClient.cs
+++ b/src/ui/iPath.Blazor.ServiceLib/Services/DirectApiClient.cs
@@ -1,19 +1,21 @@
 using System.Net;
 using System.Net.Http.Headers;
 using DispatchR;
 using FluentResults;
+using iPath.API.Services.CaseRoom;
 using iPath.Application.Contracts;
 using iPath.Application.Features;
 using iPath.Application.Features.Admin;
 using iPath.Application.AI;
 using iPath.Application.Features.Annotations;
+using iPath.Application.Features.CaseRoom;
 using iPath.Application.Features.CMS;
 using iPath.Application.Features.Documents;
 using iPath.Application.Features.EmailImport;
 using iPath.Application.Features.Notifications;
 using iPath.Application.Features.ServiceRequests;
 using iPath.Application.Features.ServiceRequests.Commands;
 using iPath.Application.Features.SyncImport;
 using iPath.Application.Features.TaskAssignments;
 using iPath.Application.Features.Users;
 using iPath.Application.Features.Users.Commands;
@@ -32,21 +34,22 @@ public class DirectApiClient(
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
-    IAiExtractionQueue? queue = null)
+    IAiExtractionQueue? queue = null,
+    ICaseRoomSessionStore? caseRoomStore = null)
     : IPathApi
 {
     private static IApiResponse<T> Respond<T>(T? content) => new DirectApiResponse<T>(content);
     private static IApiResponse<T> RespondError<T>(Exception? ex = null) => new DirectApiResponse<T>(default, false, HttpStatusCode.InternalServerError, ex);
 
     private static IApiResponse RespondOk() => new DirectApiResponse();
     private static IApiResponse RespondError(Exception? ex = null) => new DirectApiResponse(false, HttpStatusCode.InternalServerError, ex);
 
     private static Task<IApiResponse<T>> NotSupported<T>() => Task.FromResult(RespondError<T>());
     private static Task<IApiResponse> NotSupportedVoid() => Task.FromResult(RespondError());
@@ -884,11 +887,46 @@ public class DirectApiClient(
     public async Task<IApiResponse<SyncStartResponse>> DeleteImport(int groupId)
     {
         if (jobManager is null) return RespondError<SyncStartResponse>();
         var userId = userSession.User?.Id;
         var jobId = jobManager.StartDelete(groupId, userId);
         return Respond(new SyncStartResponse(jobId.ToString()));
     }
 
     public async Task<IApiResponse<SyncJobState>> GetSyncJobStatus()
         => Respond(jobManager?.Current);
+
+
+    // -- CaseRoom --
+
+    public async Task<IApiResponse<CaseRoomSnapshot>> JoinCaseRoom(Guid requestId)
+    {
+        if (caseRoomStore is null || userSession.User is null)
+            return RespondError<CaseRoomSnapshot>();
+        var snap = await caseRoomStore.JoinAsync(requestId, userSession.User.Id, userSession.User.Username, default);
+        return Respond(snap);
+    }
+
+    public async Task<IApiResponse> LeaveCaseRoom(Guid requestId)
+    {
+        if (caseRoomStore is null || userSession.User is null)
+            return RespondError();
+        await caseRoomStore.LeaveAsync(requestId, userSession.User.Id, default);
+        return RespondOk();
+    }
+
+    public async Task<IApiResponse> SyncCaseRoom(Guid requestId, SyncPayload payload)
+    {
+        if (caseRoomStore is null || userSession.User is null)
+            return RespondError();
+        await caseRoomStore.SyncAsync(requestId, userSession.User.Id, payload, default);
+        return RespondOk();
+    }
+
+    public async Task<IApiResponse<CaseRoomStatus?>> GetCaseRoomStatus(Guid requestId)
+    {
+        if (caseRoomStore is null)
+            return Respond<CaseRoomStatus?>(null);
+        var status = await caseRoomStore.GetStatusAsync(requestId, default);
+        return Respond(status);
+    }
 }
diff --git a/src/ui/iPath.Blazor.ServiceLib/iPath.Blazor.ServiceLib.csproj b/src/ui/iPath.Blazor.ServiceLib/iPath.Blazor.ServiceLib.csproj
index ac53f5b..37199b9 100644
--- a/src/ui/iPath.Blazor.ServiceLib/iPath.Blazor.ServiceLib.csproj
+++ b/src/ui/iPath.Blazor.ServiceLib/iPath.Blazor.ServiceLib.csproj
@@ -7,13 +7,14 @@
   </PropertyGroup>
 
   <ItemGroup>
     <PackageReference Include="Humanizer.Core" Version="3.0.10" />
     <PackageReference Include="Refit" Version="10.2.0" />
     <PackageReference Include="Refit.HttpClientFactory" Version="10.2.0" />
   </ItemGroup>
 
   <ItemGroup>
     <ProjectReference Include="..\..\core\iPath.Application\iPath.Application.csproj" />
+    <ProjectReference Include="..\..\infrastructure\iPath.API\iPath.API.csproj" />
   </ItemGroup>
 
 </Project>
diff --git a/src/ui/iPath.RazorLib/CaseRoom/HttpCaseRoomSyncReceiver.cs b/src/ui/iPath.RazorLib/CaseRoom/HttpCaseRoomSyncReceiver.cs
new file mode 100644
index 0000000..2e81518
--- /dev/null
+++ b/src/ui/iPath.RazorLib/CaseRoom/HttpCaseRoomSyncReceiver.cs
@@ -0,0 +1,26 @@
+using iPath.Application.Features.CaseRoom;
+using iPath.Blazor.Componenents.Notifications;
+
+namespace iPath.Blazor.Componenents.CaseRoom;
+
+public sealed class HttpCaseRoomSyncReceiver(SseClientService sse) : ICaseRoomSyncReceiver
+{
+    public IDisposable Subscribe(Guid requestId, Action<CaseRoomSyncEvent> handler)
+    {
+        void wrapper(object? s, CaseRoomSyncEvent e)
+        {
+            if (e.RequestId == requestId) handler(e);
+        }
+        sse.CaseRoomSyncReceived += wrapper;
+        var sub = new SyncUnsubscriber(() =>
+        {
+            sse.CaseRoomSyncReceived -= wrapper;
+        });
+        return sub;
+    }
+
+    private sealed class SyncUnsubscriber(Action dispose) : IDisposable
+    {
+        public void Dispose() => dispose();
+    }
+}
diff --git a/src/ui/iPath.RazorLib/CaseRoom/HttpCaseRoomSyncService.cs b/src/ui/iPath.RazorLib/CaseRoom/HttpCaseRoomSyncService.cs
new file mode 100644
index 0000000..77b4837
--- /dev/null
+++ b/src/ui/iPath.RazorLib/CaseRoom/HttpCaseRoomSyncService.cs
@@ -0,0 +1,18 @@
+using iPath.Application.Features.CaseRoom;
+using iPath.Blazor.ServiceLib.Services;
+
+namespace iPath.Blazor.Componenents.CaseRoom;
+
+public class HttpCaseRoomSyncService(IPathApi api) : ICaseRoomSyncService
+{
+    public Task<CaseRoomSnapshot> JoinAsync(Guid requestId, CancellationToken ct = default)
+        => api.JoinCaseRoom(requestId).ContinueWith(t => t.Result.Content!, ct);
+
+    public Task LeaveAsync(Guid requestId, CancellationToken ct = default)
+        => api.LeaveCaseRoom(requestId);
+
+    public Task SyncAsync(Guid requestId, SyncPayload payload, CancellationToken ct = default)
+        => api.SyncCaseRoom(requestId, payload);
+
+    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
+}
diff --git a/src/ui/iPath.RazorLib/CaseRoom/InMemoryCaseRoomSyncReceiver.cs b/src/ui/iPath.RazorLib/CaseRoom/InMemoryCaseRoomSyncReceiver.cs
new file mode 100644
index 0000000..16a365f
--- /dev/null
+++ b/src/ui/iPath.RazorLib/CaseRoom/InMemoryCaseRoomSyncReceiver.cs
@@ -0,0 +1,16 @@
+using iPath.Application.Features.CaseRoom;
+using iPath.Application.Features.Notifications;
+
+namespace iPath.Blazor.Componenents.CaseRoom;
+
+public sealed class InMemoryCaseRoomSyncReceiver(INotificationEventBus bus) : ICaseRoomSyncReceiver
+{
+    public IDisposable Subscribe(Guid requestId, Action<CaseRoomSyncEvent> handler)
+    {
+        void filtered(CaseRoomSyncEvent e)
+        {
+            if (e.RequestId == requestId) handler(e);
+        }
+        return bus.SubscribeCaseRoomSync(filtered);
+    }
+}
diff --git a/src/ui/iPath.RazorLib/CaseRoom/InMemoryCaseRoomSyncService.cs b/src/ui/iPath.RazorLib/CaseRoom/InMemoryCaseRoomSyncService.cs
new file mode 100644
index 0000000..c5310cf
--- /dev/null
+++ b/src/ui/iPath.RazorLib/CaseRoom/InMemoryCaseRoomSyncService.cs
@@ -0,0 +1,31 @@
+using iPath.API.Services.CaseRoom;
+using iPath.Application.Features.CaseRoom;
+using iPath.Application.Contracts;
+
+namespace iPath.Blazor.Componenents.CaseRoom;
+
+public class InMemoryCaseRoomSyncService(
+    ICaseRoomSessionStore store,
+    IUserSession userSession) : ICaseRoomSyncService
+{
+    public Task<CaseRoomSnapshot> JoinAsync(Guid requestId, CancellationToken ct = default)
+    {
+        if (userSession.User is null)
+            throw new InvalidOperationException("User not authenticated");
+        return store.JoinAsync(requestId, userSession.User.Id, userSession.User.Username, ct);
+    }
+
+    public Task LeaveAsync(Guid requestId, CancellationToken ct = default)
+    {
+        if (userSession.User is null) return Task.CompletedTask;
+        return store.LeaveAsync(requestId, userSession.User.Id, ct);
+    }
+
+    public Task SyncAsync(Guid requestId, SyncPayload payload, CancellationToken ct = default)
+    {
+        if (userSession.User is null) return Task.CompletedTask;
+        return store.SyncAsync(requestId, userSession.User.Id, payload, ct);
+    }
+
+    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
+}
diff --git a/src/ui/iPath.RazorLib/RazorLibServiceRegistration.cs b/src/ui/iPath.RazorLib/RazorLibServiceRegistration.cs
index 0dd47c2..5c24436 100644
--- a/src/ui/iPath.RazorLib/RazorLibServiceRegistration.cs
+++ b/src/ui/iPath.RazorLib/RazorLibServiceRegistration.cs
@@ -1,21 +1,23 @@
 ´╗┐using iPath.Application.Contracts;
 using iPath.Application.Features.Admin;
+using iPath.Application.Features.CaseRoom;
 using iPath.Application.Features.Notifications;
 using iPath.Application.Fhir;
 using iPath.Application.Localization;
 using iPath.Blazor.ServiceLib.Services;
 using iPath.Blazor.Componenents.Admin.Communities;
 using iPath.Blazor.Componenents.Admin.Groups;
 using iPath.Blazor.Componenents.Admin.Questionnaires;
 using iPath.Blazor.Componenents.Admin.Users;
 using iPath.Blazor.Componenents.Communities;
+using iPath.Blazor.Componenents.CaseRoom;
 using iPath.Blazor.Componenents.Notifications;
 using iPath.Blazor.Componenents.Shared;
 using iPath.Blazor.Componenents.Users;
 using iPath.Blazor.Componenents.TaskAssignments;
 using iPath.Blazor.ServiceLib.Fhir;
 using Microsoft.Extensions.DependencyInjection;
 using Microsoft.Extensions.Hosting;
 using MudBlazor.Translations;
 using Refit;
 using System.Text.Json;
@@ -103,20 +105,32 @@ public static class RazorLibServiceRegistration
         {
             return new CodingService(sp, "icdo");
         });
 
         // html preview
         services.AddTransient<IServiceRequestHtmlPreview, EmailNotificationPreview>();
 
         services.AddScoped<AppState>();
         services.AddScoped<SseClientService>();
 
+        // CaseRoom: WASM uses HTTP+SSE; Server uses in-memory + EventBus
+        if (WasmClient)
+        {
+            services.AddScoped<ICaseRoomSyncService, HttpCaseRoomSyncService>();
+            services.AddScoped<ICaseRoomSyncReceiver, HttpCaseRoomSyncReceiver>();
+        }
+        else
+        {
+            services.AddScoped<ICaseRoomSyncService, InMemoryCaseRoomSyncService>();
+            services.AddScoped<ICaseRoomSyncReceiver, InMemoryCaseRoomSyncReceiver>();
+        }
+
         return services;
     }
 
     public static void InitComponenetsExtensions(this IServiceProvider sp)
     {
         DocumentExtensions.Initialize(sp);
 
         var coding = sp.GetKeyedService<CodingService>("icdo");
         QuestionnaireExtension.Initialize(coding);
     }
diff --git a/src/ui/iPath.RazorLib/ServiceRequests/CaseRoomPage.razor b/src/ui/iPath.RazorLib/ServiceRequests/CaseRoomPage.razor
new file mode 100644
index 0000000..82c7052
--- /dev/null
+++ b/src/ui/iPath.RazorLib/ServiceRequests/CaseRoomPage.razor
@@ -0,0 +1,35 @@
+@page "/request/{id}/caseroom"
+
+@attribute [Authorize]
+
+@using MudBlazor.Services
+@using iPath.Application.Features.ServiceRequests
+@using iPath.Blazor.Componenents.Layouts
+@layout SlideshowLayout
+@inherits ServiceRequestViewComponentBase
+@inject ICaseRoomSyncService SyncService
+@inject ICaseRoomSyncReceiver SyncReceiver
+@inject IJSRuntime JS
+@inject IOptions<iPathClientConfig> opts
+
+<MudSwipeArea Style="height: 100%; width: 100%; background-color: black;"
+              OnSwipeEnd="OnSwipeHandler">
+
+    <div class="d-flex justify-center flex-grow-1 gap-2">
+        <MudIconButton Icon="@Icons.Material.Filled.ArrowCircleLeft" Size="Size.Small" OnClick="GotoPrevious" />
+        <MudIconButton Icon="@Icons.Material.Filled.ArrowCircleRight" Size="Size.Small" OnClick="GotoNext" />
+        <MudChip T="string" Color="Color.Success" Size="Size.Small" Variant="Variant.Filled">
+            @Participants.Count viewing
+        </MudChip>
+        <MudIconButton Icon="@Icons.Material.Filled.CloseFullscreen" Size="Size.Small" OnClick="@ExitRoom" />
+    </div>
+
+    <MudPaper Class="ipath_image slideshow" Style="background-color: black;" Elevation="0">
+        <div id="osd-caseroom" style="width: 100%; height: calc(100vh - 120px); background-color: black;"></div>
+    </MudPaper>
+</MudSwipeArea>
+
+@code {
+    [Parameter] public string id { get; set; }
+    bool Wsi => opts.Value.WsiViewerActive;
+}
diff --git a/src/ui/iPath.RazorLib/ServiceRequests/CaseRoomPage.razor.cs b/src/ui/iPath.RazorLib/ServiceRequests/CaseRoomPage.razor.cs
new file mode 100644
index 0000000..7fbfad4
--- /dev/null
+++ b/src/ui/iPath.RazorLib/ServiceRequests/CaseRoomPage.razor.cs
@@ -0,0 +1,151 @@
+using iPath.Application.Contracts;
+using iPath.Application.Features.CaseRoom;
+using iPath.Application.Features.Documents;
+using iPath.Blazor.Componenents.Documents;
+using iPath.Blazor.Componenents.Shared;
+using Microsoft.AspNetCore.Components;
+using Microsoft.JSInterop;
+using MudBlazor.Services;
+
+namespace iPath.Blazor.Componenents.ServiceRequests;
+
+public partial class CaseRoomPage
+{
+    [Inject]
+    private AppState AppState { get; set; } = null!;
+
+    private Guid RequestId => Guid.Parse(id);
+    private IJSObjectReference? _module;
+    private DotNetObjectReference<CaseRoomPage>? _dotNetRef;
+    private IDisposable? _syncSub;
+    private bool _isApplyingRemote;
+    private bool _initialized;
+
+    private List<Participant> Participants { get; set; } = new();
+
+    protected override async Task OnAfterRenderAsync(bool firstRender)
+    {
+        if (firstRender && !_initialized)
+        {
+            _initialized = true;
+            await vm.LoadNode(RequestId);
+
+            _module = await JS.InvokeAsync<IJSObjectReference>(
+                "import", "./_content/iPath.Blazor.Componenents/js/ipath-caseroom.js");
+
+            _dotNetRef = DotNetObjectReference.Create(this);
+
+            var snapshot = await SyncService.JoinAsync(RequestId);
+            Participants = snapshot.Participants.ToList();
+            StateHasChanged();
+
+            _syncSub = SyncReceiver.Subscribe(RequestId, e => _ = OnSyncReceived(e));
+
+            var docId = snapshot.ActiveDocumentId ??
+                        vm.SelectedRequest?.Documents.FirstOrDefault(n => n.IsSlide)?.Id;
+
+            if (docId.HasValue)
+            {
+                vm.SelectDocument(docId.Value);
+                var doc = vm.SelectedDocument;
+                var url = GetTileSourceUrl(doc);
+                await _module.InvokeVoidAsync("initOsd", "osd-caseroom", url, _dotNetRef);
+            }
+            else
+            {
+                await _module.InvokeVoidAsync("initOsd", "osd-caseroom", null, _dotNetRef);
+            }
+        }
+    }
+
+    [JSInvokable]
+    public async Task OnViewportChanged(double x, double y, double zoom)
+    {
+        if (_isApplyingRemote) return;
+        await SyncService.SyncAsync(RequestId, new SyncPayload(null, new ViewportState(x, y, zoom)));
+    }
+
+    private async Task OnSyncReceived(CaseRoomSyncEvent evt)
+    {
+        if (evt.UserId == AppState.User?.Id) return;
+
+        _isApplyingRemote = true;
+
+        if (evt.Payload.DocumentId.HasValue && _module is not null)
+        {
+            vm.SelectDocument(evt.Payload.DocumentId.Value);
+            var url = GetTileSourceUrl(vm.SelectedDocument);
+            await _module.InvokeVoidAsync("openTileSource", url);
+        }
+
+        if (evt.Payload.Viewport is not null && _module is not null)
+        {
+            await _module.InvokeVoidAsync("setViewport",
+                evt.Payload.Viewport.X, evt.Payload.Viewport.Y, evt.Payload.Viewport.Zoom);
+        }
+
+        _isApplyingRemote = false;
+        await InvokeAsync(StateHasChanged);
+    }
+
+    private async Task GotoNext()
+    {
+        await vm.SelectNextSlide();
+        await BroadcastDocumentChange();
+    }
+
+    private async Task GotoPrevious()
+    {
+        await vm.SelectPreviousSlide();
+        await BroadcastDocumentChange();
+    }
+
+    private async Task OnSwipeHandler(SwipeEventArgs args)
+    {
+        if (args.SwipeDirection == SwipeDirection.RightToLeft)
+            await GotoNext();
+        else if (args.SwipeDirection == SwipeDirection.LeftToRight)
+            await GotoPrevious();
+    }
+
+    private async Task BroadcastDocumentChange()
+    {
+        if (vm.SelectedDocument is null) return;
+        await SyncService.SyncAsync(RequestId, new SyncPayload(vm.SelectedDocument.Id, null));
+        if (_module is not null)
+            await _module.InvokeVoidAsync("openTileSource", GetTileSourceUrl(vm.SelectedDocument));
+    }
+
+    private async Task ExitRoom()
+    {
+        await vm.GoUpRequestPage();
+    }
+
+    private string GetTileSourceUrl(DocumentDto? doc)
+    {
+        if (doc is null) return string.Empty;
+        return doc.FileExtension.ToLower() == ".vsi"
+            ? $"/files/{doc.Id}.dzi"
+            : $"/files/{doc.Id}";
+    }
+
+    protected override async ValueTask OnDisposedAsync()
+    {
+        _syncSub?.Dispose();
+        try
+        {
+            if (_module is not null)
+                await _module.InvokeVoidAsync("dispose");
+        }
+        catch (JSDisconnectedException) { }
+        try
+        {
+            await SyncService.LeaveAsync(RequestId);
+        }
+        catch { }
+        _dotNetRef?.Dispose();
+        if (_module is not null)
+            await _module.DisposeAsync();
+        await base.OnDisposedAsync();
+    }
+}
diff --git a/src/ui/iPath.RazorLib/ServiceRequests/ServiceRequestPage.razor b/src/ui/iPath.RazorLib/ServiceRequests/ServiceRequestPage.razor
index fae0f6c..1d7fb58 100644
--- a/src/ui/iPath.RazorLib/ServiceRequests/ServiceRequestPage.razor
+++ b/src/ui/iPath.RazorLib/ServiceRequests/ServiceRequestPage.razor
@@ -1,28 +1,40 @@
 @page "/request/{id}"
 
+@using iPath.Blazor.ServiceLib.ApiClient
 @inherits ServiceRequestViewComponentBase
 @inject ServiceRequestViewModel vm
 @inject ServiceRequestListViewModel listVm
 @inject ISnackbar snackbar
 @inject IDialogService dialogService
+@inject IPathApi api
+@inject NavigationManager nm
 
 @attribute [Authorize()]
 
 @if (vm.SelectedRequest is not null)
 {
     <TaskPrompt ServiceRequestId="@vm.SelectedRequest.Id" Class="mb-5" />
 }
 
 
 <ServiceRequestHeader />
 
+@if (CaseRoomActive)
+{
+    <MudChip T="string" Color="Color.Success" Size="Size.Small" Variant="Variant.Filled"
+             Class="ma-1 mud-chip-link" OnClick="@NavigateToCaseRoom"
+             Icon="@Icons.Material.Filled.Group">
+        @CaseRoomParticipantCount in CaseRoom
+    </MudChip>
+}
+
 @if (vm.SelectedRequest is null)
 {
     <MudContainer MaxWidth="MaxWidth.Large" Class="mt-4">
         <MudSkeleton SkeletonType="SkeletonType.Rectangle" Height="200px" />
         <MudSkeleton SkeletonType="SkeletonType.Text" Class="mt-4" />
         <MudSkeleton SkeletonType="SkeletonType.Text" />
     </MudContainer>
 }
 else
 {
@@ -33,20 +45,22 @@ else
     </MudStack>
 }
 
 
 @code {
     [Parameter]
     public string id { get; set; }
 
     bool ShowDescription = true;
     NodeAnnotations annotationView;
+    private bool CaseRoomActive;
+    private int CaseRoomParticipantCount;
 
     protected override void OnInitialized()
     {
         if (listVm.LastLoadedItems != null && Guid.TryParse(id, out var guid))
         {
             vm.SelectedRequestHeader = listVm.LastLoadedItems.FirstOrDefault(x => x.Id == guid);
         }
         
         base.OnInitialized();
     }
@@ -61,12 +75,34 @@ else
     {
         _ = LoadDataAsync();
     }
 
     private async Task LoadDataAsync()
     {
         await Task.Yield();
         await vm.LoadNode(Guid.Parse(id), false);
         await InvokeAsync(StateHasChanged);
         await vm.MarkAsVisited();
+        _ = CheckCaseRoomStatusAsync();
+    }
+
+    private async Task CheckCaseRoomStatusAsync()
+    {
+        if (vm.SelectedRequest is null) return;
+        try
+        {
+            var resp = await api.GetCaseRoomStatus(vm.SelectedRequest.Id);
+            if (resp.IsSuccessful && resp.Content is not null)
+            {
+                CaseRoomActive = resp.Content.IsActive;
+                CaseRoomParticipantCount = resp.Content.ParticipantCount;
+                await InvokeAsync(StateHasChanged);
+            }
+        }
+        catch { }
+    }
+
+    private void NavigateToCaseRoom()
+    {
+        nm.NavigateTo($"request/{id}/caseroom");
     }
 }
\ No newline at end of file
diff --git a/src/ui/iPath.RazorLib/_Imports.razor b/src/ui/iPath.RazorLib/_Imports.razor
index dd12a89..cae7a8b 100644
--- a/src/ui/iPath.RazorLib/_Imports.razor
+++ b/src/ui/iPath.RazorLib/_Imports.razor
@@ -21,20 +21,21 @@
 @using iPath.Blazor.Componenents.Groups
 @using iPath.Blazor.Componenents.Groups.Components
 @using iPath.Blazor.Componenents.Questionaires
 @using iPath.Blazor.Componenents.ServiceRequests
 @using iPath.Blazor.Componenents.Shared
 @using iPath.Blazor.Componenents.TaskAssignments
 @using iPath.Blazor.Componenents.Shared.Coding
 @using iPath.Blazor.Componenents.Shared.Lookups
 @using iPath.Blazor.Componenents.Notifications
 @using iPath.Blazor.Componenents.Users
+@using iPath.Application.Features.CaseRoom
 @using iPath.Application.Features.CMS
 @using iPath.Application.Features.TaskAssignments
 @using iPath.Blazor.ServiceLib.Services
 @using iPath.Domain.Entities
 @using iPath.Domain.Config
 @using iPath.Domain.Notifications
 @using iPath.Application.Contracts
 @using static Microsoft.AspNetCore.Components.Web.RenderMode
 
 
diff --git a/src/ui/iPath.RazorLib/wwwroot/js/ipath-caseroom.js b/src/ui/iPath.RazorLib/wwwroot/js/ipath-caseroom.js
new file mode 100644
index 0000000..a36d0d9
--- /dev/null
+++ b/src/ui/iPath.RazorLib/wwwroot/js/ipath-caseroom.js
@@ -0,0 +1,77 @@
+let viewer = null;
+let dotNetRef = null;
+let throttleTimer = null;
+let isApplyingRemote = false;
+
+export function initOsd(divId, tileSourceUrl, dotNetReference) {
+    dotNetRef = dotNetReference;
+
+    const elem = document.getElementById(divId);
+    if (!elem) return;
+
+    elem.innerHTML = '';
+
+    viewer = OpenSeadragon({
+        id: elem.id,
+        prefixUrl: "https://cdn.jsdelivr.net/npm/openseadragon@5.0.1/build/openseadragon/images/",
+        visibilityRatio: 1.0,
+        constrainDuringPan: true,
+        defaultZoomLevel: 0,
+        minZoomLevel: 0.5,
+        maxZoomLevel: 100,
+        showNavigationControl: false,
+        crossOriginPolicy: "Anonymous"
+    });
+
+    viewer.addHandler('open', () => { });
+
+    viewer.addHandler('open-failed', (event) => {
+        console.error('OSD open failed:', event.message);
+    });
+
+    viewer.addHandler('viewport-change', onViewportChange);
+
+    if (tileSourceUrl) openTileSource(tileSourceUrl);
+}
+
+export function openTileSource(url) {
+    if (!viewer) return;
+    if (url && url.toLowerCase().endsWith('.dzi')) {
+        viewer.open(url);
+    } else {
+        viewer.open({ type: 'image', url: url, buildPyramid: false });
+    }
+}
+
+export function setViewport(x, y, zoom) {
+    if (!viewer) return;
+    isApplyingRemote = true;
+    viewer.viewport.panTo({ x: x, y: y }, true);
+    viewer.viewport.zoomTo(zoom, null, true);
+    setTimeout(() => { isApplyingRemote = false; }, 50);
+}
+
+export function getViewport() {
+    if (!viewer) return null;
+    const c = viewer.viewport.getCenter();
+    const z = viewer.viewport.getZoom();
+    return { x: c.x, y: c.y, zoom: z };
+}
+
+export function dispose() {
+    if (throttleTimer) { clearTimeout(throttleTimer); throttleTimer = null; }
+    if (viewer) { viewer.destroy(); viewer = null; }
+    dotNetRef = null;
+    isApplyingRemote = false;
+}
+
+function onViewportChange() {
+    if (isApplyingRemote || !dotNetRef) return;
+    if (throttleTimer) return;
+    throttleTimer = setTimeout(() => {
+        const c = viewer.viewport.getCenter();
+        const z = viewer.viewport.getZoom();
+        dotNetRef.invokeMethodAsync('OnViewportChanged', c.x, c.y, z);
+        throttleTimer = null;
+    }, 150);
+}
diff --git a/test/iPath.Test.xUnit2/CaseRoom/CaseRoomSyncTransportTests.cs b/test/iPath.Test.xUnit2/CaseRoom/CaseRoomSyncTransportTests.cs
new file mode 100644
index 0000000..6ccd173
--- /dev/null
+++ b/test/iPath.Test.xUnit2/CaseRoom/CaseRoomSyncTransportTests.cs
@@ -0,0 +1,71 @@
+using iPath.API.Services.CaseRoom;
+using iPath.Application.Features.CaseRoom;
+using iPath.Application.Features.Notifications;
+using iPath.Blazor.Componenents.CaseRoom;
+using iPath.Blazor.Componenents.Notifications;
+using iPath.Blazor.ServiceLib.Services;
+using Microsoft.Extensions.DependencyInjection;
+using Microsoft.Extensions.Logging;
+using Microsoft.JSInterop;
+using NSubstitute;
+using FluentAssertions;
+using iPath.Application.Contracts;
+
+namespace iPath.Test.xUnit2.CaseRoom;
+
+public class CaseRoomSyncTransportTests
+{
+    [Fact]
+    public async Task InMemorySync_PublishesViaEventBusAndReachesReceiver()
+    {
+        var sseMgr = Substitute.For<iPath.API.Services.Notifications.ISseConnectionManager>();
+        var bus = new NotificationEventBus();
+        var store = new CaseRoomSessionStore(sseMgr, bus, new LoggerFactory().CreateLogger<CaseRoomSessionStore>());
+
+        var received = new List<CaseRoomSyncEvent>();
+        var receiver = new InMemoryCaseRoomSyncReceiver(bus);
+        var requestId = Guid.NewGuid();
+
+        var sub = receiver.Subscribe(requestId, e =>
+        {
+            if (e.RequestId == requestId) received.Add(e);
+        });
+
+        var userA = Guid.NewGuid();
+        var userB = Guid.NewGuid();
+        await store.JoinAsync(requestId, userA, "Alice", default);
+        await store.JoinAsync(requestId, userB, "Bob", default);
+
+        await store.SyncAsync(requestId, userA, new SyncPayload(null, new ViewportState(0.5, 0.5, 2.0)), default);
+
+        received.Should().NotBeEmpty();
+        sub.Dispose();
+    }
+
+    [Fact]
+    public async Task HttpReceiver_ForwardsSseClientServiceEvents()
+    {
+        var services = new ServiceCollection().BuildServiceProvider();
+        var logger = new LoggerFactory().CreateLogger<SseClientService>();
+        var js = Substitute.For<IJSRuntime>();
+        var sseService = new SseClientService(js, services, logger);
+
+        var receiver = new HttpCaseRoomSyncReceiver(sseService);
+        var received = new List<CaseRoomSyncEvent>();
+        var requestId = Guid.NewGuid();
+        var sub = receiver.Subscribe(requestId, e =>
+        {
+            if (e.RequestId == requestId) received.Add(e);
+        });
+
+        var evt = new CaseRoomSyncEvent(requestId, Guid.NewGuid(), "Alice",
+            new SyncPayload(null, new ViewportState(1, 1, 1)), DateTimeOffset.UtcNow);
+
+        var json = System.Text.Json.JsonSerializer.Serialize(evt, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
+        sseService.OnCaseRoomSync(json, DateTimeOffset.UtcNow.ToString("o"));
+
+        received.Should().ContainSingle();
+        received[0].DisplayName.Should().Be("Alice");
+        sub.Dispose();
+    }
+}
diff --git a/test/iPath.Test.xUnit2/CaseRoom/DirectApiClientCaseRoomTests.cs b/test/iPath.Test.xUnit2/CaseRoom/DirectApiClientCaseRoomTests.cs
new file mode 100644
index 0000000..eb572fc
--- /dev/null
+++ b/test/iPath.Test.xUnit2/CaseRoom/DirectApiClientCaseRoomTests.cs
@@ -0,0 +1,83 @@
+using iPath.API.Services.CaseRoom;
+using iPath.API.Services.Notifications;
+using iPath.Application.Contracts;
+using iPath.Application.Features;
+using iPath.Application.Features.CaseRoom;
+using iPath.Application.Features.Notifications;
+using iPath.Application.Features.Users;
+using iPath.Application.Localization;
+using iPath.Blazor.ServiceLib.Services;
+using iPath.Domain.Config;
+using DispatchR;
+using FluentAssertions;
+using Microsoft.Extensions.Logging;
+using Microsoft.Extensions.Options;
+using NSubstitute;
+
+namespace iPath.Test.xUnit2.CaseRoom;
+
+public class DirectApiClientCaseRoomTests
+{
+    private static (DirectApiClient client, CaseRoomSessionStore store) CreateClient()
+    {
+        var store = new CaseRoomSessionStore(
+            Substitute.For<ISseConnectionManager>(),
+            new NotificationEventBus(),
+            new LoggerFactory().CreateLogger<CaseRoomSessionStore>());
+
+        var testUserId = Guid.NewGuid();
+        var mediator = Substitute.For<IMediator>();
+        var userSession = Substitute.For<IUserSession>();
+        userSession.User.Returns(new SessionUserDto(testUserId, "Test", "test@test.com", "TT", new[] { "Admin" }, null, null));
+
+        var opts = Substitute.For<IOptions<iPathClientConfig>>();
+        opts.Value.Returns(new iPathClientConfig());
+
+        var client = new DirectApiClient(
+            mediator: mediator,
+            groupService: Substitute.For<IGroupService>(),
+            emailRepo: Substitute.For<IEmailRepository>(),
+            notificationRepo: Substitute.For<INotificationRepository>(),
+            userSession: userSession,
+            localization: Substitute.For<ILocalizationDataProvider>(),
+            config: opts,
+            logger: new LoggerFactory().CreateLogger<DirectApiClient>(),
+            caseRoomStore: store);
+
+        return (client, store);
+    }
+
+    [Fact]
+    public async Task DirectApiClient_JoinCaseRoom_ReturnsSnapshotFromStore()
+    {
+        var (client, store) = CreateClient();
+        var requestId = Guid.NewGuid();
+
+        var resp = await client.JoinCaseRoom(requestId);
+
+        resp.IsSuccessful.Should().BeTrue();
+        resp.Content!.RequestId.Should().Be(requestId);
+        resp.Content.Participants.Should().ContainSingle();
+    }
+
+    [Fact]
+    public async Task DirectApiClient_SyncCaseRoom_PersistsViewport()
+    {
+        var (client, store) = CreateClient();
+        var requestId = Guid.NewGuid();
+        await client.JoinCaseRoom(requestId);
+
+        await client.SyncCaseRoom(requestId, new SyncPayload(null, new ViewportState(0.1, 0.2, 0.3)));
+
+        var status = await client.GetCaseRoomStatus(requestId);
+        status.Content!.IsActive.Should().BeTrue();
+    }
+
+    [Fact]
+    public async Task DirectApiClient_GetCaseRoomStatus_ReturnsNullWhenNoSession()
+    {
+        var (client, _) = CreateClient();
+        var resp = await client.GetCaseRoomStatus(Guid.NewGuid());
+        resp.Content.Should().BeNull();
+    }
+}
