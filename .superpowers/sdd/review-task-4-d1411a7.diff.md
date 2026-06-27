## Commit list

d1411a7 feat(caseroom): add API endpoints for join/leave/sync/status

## Stat summary

 .../iPath.API/APIServicesRegistration.cs           |  4 ++
 .../iPath.API/Endpoints/CaseRoomEndpoints.cs       | 63 ++++++++++++++++++++++
 src/infrastructure/iPath.API/MapEndpoints.cs       |  3 +-
 3 files changed, 69 insertions(+), 1 deletion(-)

## Full diff

diff --git a/src/infrastructure/iPath.API/APIServicesRegistration.cs b/src/infrastructure/iPath.API/APIServicesRegistration.cs
index 393356a..0383881 100644
--- a/src/infrastructure/iPath.API/APIServicesRegistration.cs
+++ b/src/infrastructure/iPath.API/APIServicesRegistration.cs
@@ -1,12 +1,13 @@
 using DispatchR.Extensions;
 using iPath.API.Services;
+using iPath.API.Services.CaseRoom;
 using iPath.API.Services.Email;
 using iPath.API.Services.Email.Clients;
 using iPath.API.Services.Notifications;
 using iPath.API.Services.Notifications.Processors;
 using iPath.API.Services.Notifications.Publisher;
 using iPath.API.Services.Storage;
 using iPath.API.Services.Thumbnail;
 using iPath.API.Services.SyncImport;
 using iPath.Application.Features.SyncImport;
 using iPath.Application.Coding;
@@ -108,20 +109,23 @@ public static class APIServicesRegistration
         // Publishers: Email + InApp (SSE)
         services.AddScoped<INotificationPublisher, EmailNotificationPublisher>();
         services.AddScoped<INotificationPublisher, InAppNotificationPublisher>();
 
         // SSE Connection Manager (singleton)
         services.AddSingleton<ISseConnectionManager, SseConnectionManager>();
 
         // In-process event bus for Server-mode direct subscription (avoids browser round trip)
         services.AddSingleton<INotificationEventBus, NotificationEventBus>();
 
+        // CaseRoom session store (in-memory, transient sessions)
+        services.AddSingleton<ICaseRoomSessionStore, CaseRoomSessionStore>();
+
         services.AddHostedService<NotificationPublisher>();
         services.AddTransient<IServiceRequestHtmlPreview, EmailNotificationPreview>();
 
 
 
         // Upload Handling
         services.AddSingleton<IRemoteStorageUploadQueue, RemoteStorageUploadQueue>(ctx =>
         {
             return new RemoteStorageUploadQueue(100);
         });
diff --git a/src/infrastructure/iPath.API/Endpoints/CaseRoomEndpoints.cs b/src/infrastructure/iPath.API/Endpoints/CaseRoomEndpoints.cs
new file mode 100644
index 0000000..f1a6c08
--- /dev/null
+++ b/src/infrastructure/iPath.API/Endpoints/CaseRoomEndpoints.cs
@@ -0,0 +1,63 @@
+using iPath.API.Services.CaseRoom;
+using iPath.Application.Features.CaseRoom;
+
+namespace iPath.API;
+
+public static class CaseRoomEndpoints
+{
+    public static IEndpointRouteBuilder MapCaseRoomApi(this IEndpointRouteBuilder route)
+    {
+        var group = route.MapGroup("caseroom").RequireAuthorization();
+
+        group.MapPost("{requestId:guid}/join", async (
+            Guid requestId,
+            [FromServices] ICaseRoomSessionStore store,
+            [FromServices] IUserSession sess,
+            CancellationToken ct) =>
+        {
+            if (sess.User is null || !sess.User.IsAuthenticated)
+                return Results.Unauthorized();
+
+            var snapshot = await store.JoinAsync(requestId, sess.User.Id, sess.User.Username, ct);
+            return Results.Ok(snapshot);
+        });
+
+        group.MapPost("{requestId:guid}/leave", async (
+            Guid requestId,
+            [FromServices] ICaseRoomSessionStore store,
+            [FromServices] IUserSession sess,
+            CancellationToken ct) =>
+        {
+            if (sess.User is null || !sess.User.IsAuthenticated)
+                return Results.Unauthorized();
+
+            await store.LeaveAsync(requestId, sess.User.Id, ct);
+            return Results.NoContent();
+        });
+
+        group.MapPost("{requestId:guid}/sync", async (
+            Guid requestId,
+            [FromServices] ICaseRoomSessionStore store,
+            [FromServices] IUserSession sess,
+            SyncPayload payload,
+            CancellationToken ct) =>
+        {
+            if (sess.User is null || !sess.User.IsAuthenticated)
+                return Results.Unauthorized();
+
+            await store.SyncAsync(requestId, sess.User.Id, payload, ct);
+            return Results.NoContent();
+        });
+
+        group.MapGet("{requestId:guid}", async (
+            Guid requestId,
+            [FromServices] ICaseRoomSessionStore store,
+            CancellationToken ct) =>
+        {
+            var status = await store.GetStatusAsync(requestId, ct);
+            return status is null ? Results.NotFound() : Results.Ok(status);
+        });
+
+        return route;
+    }
+}
diff --git a/src/infrastructure/iPath.API/MapEndpoints.cs b/src/infrastructure/iPath.API/MapEndpoints.cs
index 2a342de..8145c18 100644
--- a/src/infrastructure/iPath.API/MapEndpoints.cs
+++ b/src/infrastructure/iPath.API/MapEndpoints.cs
@@ -26,21 +26,22 @@ public static class MapEndpoints
             .MapDocumentEndpoints()
             .MapNotificationApi()
             .MapQuesionnairesApi()
             .MapFhirApi()
             .MapTestApi()
             .MapStatisticsApi()
             .MapCmsApi()
             .MapGoogleProxy()
             .MapEmailImportApi()
             .MapTaskAssignmentEndpoints()
-            .MapSyncApi();
+            .MapSyncApi()
+            .MapCaseRoomApi();
 
         // OpenAPI Documentation
         var openapi = config.GetValue<bool>("OpenApi");
         if (openapi)
         {
             var cfg = new iPathClientConfig();
             config.GetSection(iPathClientConfig.ConfigName).Bind(cfg);
 
             // Use static OpenAPI file generated at build time (wwwroot/openapi/openapi.json)
             // No need for MapOpenApi() since we're using the static file
