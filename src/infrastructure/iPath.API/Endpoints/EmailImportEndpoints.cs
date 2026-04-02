using Hl7.FhirPath.Sprache;
using iPath.Application.Features.EmailImport;
using iPath.EF.Core.Database;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace iPath.API;

public static class EmailImportEndpoints
{
    public static IEndpointRouteBuilder MapEmailImportApi(this IEndpointRouteBuilder route)
    {
        var emailImport = route.MapGroup("admin/email-import")
            .WithTags("Email Import")
            .RequireAuthorization("Admin");

        emailImport.MapGet("mailboxes", async (
            [FromServices] IEmailImportService service,
            CancellationToken ct) =>
        {
            var mailboxes = await service.GetMailboxesAsync(ct);
            return TypedResults.Ok(mailboxes);
        }).Produces<IReadOnlyList<ImportMailboxSummary>>();

        emailImport.MapGet("{mailboxName}/pending", async (
            string mailboxName,
            [FromServices] IEmailImportService service,
            CancellationToken ct) =>
        {
            var emails = await service.GetPendingAsync(mailboxName, ct);
            return TypedResults.Ok(emails);
        }).Produces<IReadOnlyList<ImportEmailPreview>>();

        emailImport.MapGet("{mailboxName}/{messageId}/preview", (
            string mailboxName,
            string messageId,
            [FromServices] IEmailImportService service,
            CancellationToken ct) =>
        {
            return service.GetPreviewAsync(mailboxName, messageId, ct);
        }).Produces<ImportEmailPreview>();

        emailImport.MapPost("resolve", async ([FromBody] ResolveEmailImportQuery query,
            [FromServices] IEmailImportGroupResolver resolver,
            [FromServices] IOptions <EmailImportConfig> config, 
            CancellationToken ct) =>
        {
            var mailboxConfig = config.Value.Mailboxes.FirstOrDefault(m => m.Name == query.MailboxName);             
            var res =  await resolver.ResolveGroupAsync(mailboxConfig, query.SenderEmail, ct);
            return res.ToMinimalApiResult();
        })
            .RequireAuthorization("Admin");

        emailImport.MapPost("import", [RequestTimeout(milliseconds: 120000)] async (
            [FromBody] ImportEmailCommand command,
            [FromServices] IEmailImportService service,
            CancellationToken ct) =>
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMinutes(2));
            return await service.ImportSingleAsync(command.MailboxName, command.MessageId, command.ForceReimport, cts.Token);
        }).Produces<ImportEmailResult>();

        emailImport.MapDelete("{mailboxName}/{messageId}", async (
            string mailboxName,
            string messageId,
            [FromServices] IEmailImportService service,
            CancellationToken ct) =>
        {
            await service.DeleteAsync(mailboxName, messageId, ct);
            return TypedResults.NoContent();
        });

        /*
        emailImport.MapPost("import-all", async (
            [FromServices] IEmailImportService service,
            CancellationToken ct) =>
        {
            var results = await service.ImportAllPendingAsync(ct);
            return TypedResults.Ok(results);
        }).Produces<IReadOnlyList<ImportEmailResult>>();
        */

        emailImport.MapGet("logs", async (
            [FromServices] iPathDbContext db,
            CancellationToken ct,
            int page = 0,
            int pageSize = 50) =>
        {
            var logs = await db.Set<EmailImportLog>()
                .OrderByDescending(l => l.ProcessedOn)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
            return TypedResults.Ok(logs);
        }).Produces<List<EmailImportLog>>();

        return route;
    }
}