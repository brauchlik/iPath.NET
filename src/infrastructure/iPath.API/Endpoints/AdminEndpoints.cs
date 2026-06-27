using iPath.Application.Contracts;
using iPath.Application.Features.Notifications;
using iPath.Application.Features.Users;
using iPath.Application.Localization;
using System.ComponentModel;
using System.Data;
using System.Linq.Dynamic.Core;
using iPath.API.EndpointFilters;
using iPath.Domain.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using iPath.Application.Features.Admin;
using iPath.Application.AI;
using iPath.EF.Core.Database;

namespace iPath.API;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminApi(this IEndpointRouteBuilder route)
    {
        #region "-- Internal Mailbox --"
        var mail = route.MapGroup("mail")
            .WithTags("Internal Mailbox");

        mail.AddEndpointFilterPipeline();

        mail.MapGet("list",
            ([DefaultValue(0)] int page, [DefaultValue(10)] int pagesize, [FromServices] IEmailRepository repo, CancellationToken ct)
            => repo.GetPage(new PagedQuery<EmailMessage> { Page = page, PageSize = pagesize }, ct))
            .Produces<PagedResult<EmailMessage>>();
            // .RequireAuthorization("Admin");

        mail.MapDelete("{id}", (string id, [FromServices] IEmailRepository repo, CancellationToken ct)
            => repo.Delete(Guid.Parse(id), ct))
            .RequireAuthorization("Admin");

        mail.MapDelete("all", ([FromServices] IEmailRepository repo, CancellationToken ct)
            => repo.DeleteAll(ct))
            .RequireAuthorization("Admin");

        mail.MapPut("read/{id}", (string id, [FromServices] IEmailRepository repo, CancellationToken ct)
            => repo.SetReadState(Guid.Parse(id), true, ct))
            .RequireAuthorization("Admin");

        mail.MapPut("unread/{id}", (string id, [FromServices] IEmailRepository repo, CancellationToken ct)
            => repo.SetReadState(Guid.Parse(id), false, ct))
            .RequireAuthorization("Admin");

        mail.MapPost("send", async (EmailDto msg, [FromServices] IEmailRepository repo, CancellationToken ct)
            => await repo.Create(msg.Address, msg.Subject, msg.Body, ct))
            .Produces<EmailMessage>()
            .RequireAuthorization("Admin");
        #endregion "-- Mailbox --"


        #region "-- Notifications --"
        var notify = route.MapGroup("notifications")
            .WithTags("Notifications");

        notify.MapGet("list",
            ([DefaultValue(0)] int page, [DefaultValue(10)] int pagesize, eNotificationTarget target, 
             [FromQuery] string[]? sort, [FromServices] INotificationRepository repo,
             [FromServices] IUserSession sess, CancellationToken ct)
            => repo.GetPage(new GetNotificationsQuery { Page = page, PageSize = pagesize, Target = target, Sorting = sort, UserId = sess.User?.Id }, ct))
            .Produces<PagedResult<NotificationDto>>()
            .RequireAuthorization();

        notify.MapDelete("all", ([FromServices] INotificationRepository repo, CancellationToken ct)
            => repo.DeleteAll(ct))
            .RequireAuthorization("Admin");
        #endregion "-- Notifications --"


        route.MapGet("config", (IOptions<iPathClientConfig> opts) => {
            var config = new AppSettings { iPathClientConfig = opts.Value };
            return Results.Ok(config);
        })
            .Produces<AppSettings>()
            .WithTags("Config")
            .AllowAnonymous();

        route.MapGet("admin/roles", ([FromServices] IMediator mediator, CancellationToken ct)
            => mediator.Send(new GetRolesQuery(), ct))
            .Produces<IEnumerable<RoleDto>>()
            .WithTags("Admin")
            .RequireAuthorization("Admin");


        route.MapPost("admin/events", (GetEventsQuery query, [FromServices] IMediator mediator, CancellationToken ct)
            => mediator.Send(query, ct))
            .Produces<PagedResultList<EventDto>>()
            .WithTags("Admin")
            .RequireAuthorization("Admin");


        route.MapGet("session", ([FromServices] IUserSession? sess)
            => sess is null || sess.User is null ? Results.NotFound() : Results.Ok(sess.User))
            .Produces<SessionUserDto>()
            .WithTags("Session");


        route.MapGet("translations/{lang}", (string lang, [FromServices] LocalizationFileService srv)
            => srv.GetTranslationData(lang))
            .Produces<TranslationData>()
            .WithTags("Localization");

        route.MapPost("translations/{lang}/add-missing", (string lang, List<string> keys, [FromServices] LocalizationFileService srv) =>
        {
            var data = srv.GetTranslationData(lang);
            bool updated = false;
            foreach (var key in keys)
            {
                if (data.Words.TryAdd(key, ""))
                {
                    updated = true;
                }
            }
            if (updated)
            {
                srv.SaveTranslation(data);
            }
            return Results.Ok(true);
        })
        .Produces<bool>()
        .WithTags("Localization");


        #region "-- Database Diagnostics --"
        route.MapGet("admin/database", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetDatabaseStatusQuery(), ct);
            return Results.Ok(result);
        })
            .Produces<DatabaseStatusDto>()
            .WithTags("Admin")
            .RequireAuthorization("Admin");

        route.MapGet("admin/database/tables", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetDatabaseTableCountsQuery(), ct);
            return Results.Ok(result);
        })
            .Produces<List<TableRowCountDto>>()
            .WithTags("Admin")
            .RequireAuthorization("Admin");

        route.MapGet("admin/vsi/jobs", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetVsiConversionJobsQuery(), ct);
            return Results.Ok(result);
        })
            .Produces<List<VsiConversionJobDto>>()
            .WithTags("Admin")
            .RequireAuthorization("Admin");

        route.MapGet("admin/debug/thumb/{docId}", async (string docId,
            [FromServices] iPathDbContext db,
            [FromServices] IEnumerable<IConversionPlugin> plugins,
            [FromServices] IOptions<VsiConversionConfig> vsiConfig,
            [FromServices] IOptions<iPathConfig> ipathConfig,
            CancellationToken ct) =>
        {
            var doc = await db.Documents.FindAsync([Guid.Parse(docId)], ct);
            if (doc?.File is null || string.IsNullOrEmpty(doc.File.Filename))
                return Results.NotFound();

            var ext = Path.GetExtension(doc.File.Filename);
            var plugin = plugins.FirstOrDefault(p => p.CanHandle(ext));
            if (plugin is null)
                return Results.NotFound($"No plugin for extension {ext}");

            // Find source file (same as ThumbnailWorker)
            var stagingPath = string.IsNullOrEmpty(vsiConfig.Value.StagingPath)
                ? Path.Combine(ipathConfig.Value.TempDataPath, "conversion", doc.Id.ToString())
                : Path.Combine(vsiConfig.Value.StagingPath, doc.Id.ToString());
            var sourcePath = Path.Combine(stagingPath, doc.File.Filename);
            if (!File.Exists(sourcePath))
                sourcePath = Path.Combine(ipathConfig.Value.TempDataPath, doc.Id.ToString());

            // Call the same plugin function the ThumbnailWorker uses
            var ctx = new ThumbnailContext(doc.Id, sourcePath, ipathConfig.Value.TempDataPath, 100, doc);
            var result = await plugin.CreateThumbnailAsync(ctx, ct);

            if (!result.Success || string.IsNullOrEmpty(doc.File.ThumbData))
                return Results.Problem(result.ErrorMessage ?? "Thumbnail creation failed");

            await db.SaveChangesAsync(ct);

            // Write debug _thumb.jpg for inspection
            var thumbPath = Path.Combine(ipathConfig.Value.TempDataPath, $"{doc.Id}_thumb.jpg");
            try
            {
                var bytes = Convert.FromBase64String(doc.File.ThumbData);
                await File.WriteAllBytesAsync(thumbPath, bytes, ct);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to write debug file: {ex.Message}");
            }

            return Results.File(thumbPath, "image/jpeg");
        })
            .WithTags("Admin")
            .RequireAuthorization("Admin");

        route.MapGet("admin/ai/status", async (bool? checkConnection, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAiStatusQuery(checkConnection ?? false), ct);
            return Results.Ok(result);
        })
            .Produces<AiStatusDto>()
            .WithTags("Admin")
            .RequireAuthorization("Admin");

        route.MapGet("admin/ai/lineage/{id}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAiLineageDetailQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
            .Produces<AiLineageDetailDto>()
            .WithTags("Admin")
            .RequireAuthorization("Admin");

        route.MapGet("admin/ai/lineage/by-case/{caseId}", async (Guid caseId, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAiLineageByCaseQuery(caseId), ct);
            return Results.Ok(result);
        })
            .Produces<List<AiLineageDetailDto>>()
            .WithTags("Admin")
            .RequireAuthorization("Admin");

        route.MapGet("admin/ai/translations/status", async (string locale, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetTranslationStatusQuery(locale), ct);
            return Results.Ok(result);
        })
            .Produces<TranslationStatusDto>()
            .WithTags("Admin")
            .RequireAuthorization("Admin");

        route.MapPost("admin/ai/translations/translate", async (TranslateKeysBatchCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Ok(result);
        })
            .Produces<TranslationResultDto>()
            .WithTags("Admin")
            .RequireAuthorization("Admin");

        route.MapPost("admin/ai/translations/update-key", async (UpdateTranslationKeyCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Ok(result);
        })
            .Produces<bool>()
            .WithTags("Admin")
            .RequireAuthorization("Admin");

        route.MapPost("admin/ai/enqueue/{caseId}", async (Guid caseId, [FromServices] IAiExtractionQueue queue) =>
        {
            if (queue.IsInQueue(caseId))
            {
                return Results.Ok(new AiEnqueueResult(Enqueued: false, Message: "Case is already in the AI transcription queue."));
            }

            await queue.EnqueueAsync(caseId);
            return Results.Ok(new AiEnqueueResult(Enqueued: true, Message: "Case added to AI transcription queue."));
        })
            .Produces<AiEnqueueResult>()
            .WithTags("Admin")
            .RequireAuthorization("Admin");

        route.MapPost("admin/database/migrate", async (IMediator mediator, CancellationToken ct) =>
        {
            try
            {
                var result = await mediator.Send(new ApplyDatabaseMigrationsCommand(), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to apply migrations: {ex.Message}", statusCode: 500);
            }
        })
            .Produces<DatabaseStatusDto>()
            .WithTags("Admin")
            .RequireAuthorization("Admin");
        #endregion


        #region "-- Disk Purge --"
        route.MapGet("admin/purge/deleted-documents", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetDeletedDocumentsWithFilesQuery(), ct);
            return Results.Ok(result);
        })
            .Produces<List<PurgeDocumentFileDto>>()
            .WithTags("Admin")
            .RequireAuthorization("Admin");

        route.MapPost("admin/purge/document/{documentId}", async (string documentId, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new PurgeDocumentFilesCommand(Guid.Parse(documentId)), ct);
            return Results.Ok(result);
        })
            .Produces<bool>()
            .WithTags("Admin")
            .RequireAuthorization("Admin");

        route.MapGet("admin/purge/stale-cache", async (int? daysOld, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetStaleCacheFilesQuery(daysOld ?? 7), ct);
            return Results.Ok(result);
        })
            .Produces<List<StaleCacheFileDto>>()
            .WithTags("Admin")
            .RequireAuthorization("Admin");

        route.MapPost("admin/purge/stale-cache/clean", async (int? daysOld, IMediator mediator, CancellationToken ct) =>
        {
            var deleted = await mediator.Send(new CleanStaleCacheFilesCommand(daysOld ?? 7), ct);
            return Results.Ok(deleted);
        })
            .Produces<int>()
            .WithTags("Admin")
            .RequireAuthorization("Admin");
        #endregion


        return route;
    }
}

public class AppSettings
{
    public iPathClientConfig iPathClientConfig { get; set;  } = new iPathClientConfig();
}

