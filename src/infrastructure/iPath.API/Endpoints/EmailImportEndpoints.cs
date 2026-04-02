using iPath.Application.Contracts;
using iPath.Application.Features.EmailImport;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace iPath.API;

public static class EmailImportEndpoints
{
    public static IEndpointRouteBuilder MapEmailImportApi(this IEndpointRouteBuilder route)
    {
        var emailImport = route.MapGroup("email-import")
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

        emailImport.MapPost("{mailboxName}/{messageId}/import", (
            string mailboxName,
            string messageId,
            [FromServices] IEmailImportService service,
            CancellationToken ct) =>
        {
            return service.ImportSingleAsync(mailboxName, messageId, ct);
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

        emailImport.MapPost("import-all", async (
            [FromServices] IEmailImportService service,
            CancellationToken ct) =>
        {
            var results = await service.ImportAllPendingAsync(ct);
            return TypedResults.Ok(results);
        }).Produces<IReadOnlyList<ImportEmailResult>>();

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