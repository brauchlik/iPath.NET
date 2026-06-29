using Ardalis.GuardClauses;
using Google.Apis.Drive.v3.Data;
using iPath.Application.Features.Documents;
using iPath.Application.Features.ServiceRequests.Commands;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;

namespace iPath.API.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder builder)
    {
        var grp = builder.MapGroup("documents")
            .WithTags("Documents");



        grp.MapDelete("{id}", async (string id, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new DeleteDocumentCommand(Guid.Parse(id)), ct))
            .Produces<ServiceRequestDeletedEvent>()
            .RequireAuthorization();

        grp.MapPut("update", async (UpdateDocumenttCommand request, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<bool>()
            .RequireAuthorization();


        grp.MapPut("order", async (UpdateDocumentsSortOrderCommand request, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<ChildNodeSortOrderUpdatedEvent>()
            .RequireAuthorization();


        grp.MapGet("{id}/{filename}", async (string id, string? filename, [FromServices] IMediator mediator, HttpContext ctx, CancellationToken ct) =>
        {
            if (Guid.TryParse(id, out var nodeId))
            {
                var res = await mediator.Send(new GetDocumentFileQuery(nodeId), ct);

                if (res.NotFound || !System.IO.File.Exists(res.TempFile))
                {
                    return Results.NotFound();
                }
                else if (res.AccessDenied)
                {
                    return Results.Unauthorized();
                }
                else
                {
                    var stream = new FileStream(res.TempFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return Results.File(stream, contentType: res.Info.MimeType, fileDownloadName: res.Info.Filename);
                }
            }

            return Results.BadRequest();
        })
           .RequireAuthorization()
           .Produces(StatusCodes.Status200OK)
           .Produces(StatusCodes.Status404NotFound);

        grp.MapGet("files/{*filepath}", async Task<IResult> (string filepath, [FromServices] IMediator mediator, [FromServices] IOptions<iPath.Domain.Config.iPathConfig> opts, HttpContext ctx, CancellationToken ct) =>
        {
            var parts = filepath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return Results.BadRequest();

            var target = parts[0];
            Guid? docId = null;
            if (target.EndsWith(".dzi", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(target[..^4], out var id1))
            {
                docId = id1;
            }
            else if (target.EndsWith("_files", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(target[..^6], out var id2))
            {
                docId = id2;
            }
            else if (Guid.TryParse(target, out var id3))
            {
                docId = id3;
            }

            if (!docId.HasValue) return Results.BadRequest();

            var res = await mediator.Send(new GetDocumentFileQuery(docId.Value), ct);
            if (res == null || res.NotFound) return Results.NotFound();
            if (res.AccessDenied) return Results.Unauthorized();

            var externalFilesPath = opts.Value.TempDataPath;
            if (string.IsNullOrWhiteSpace(externalFilesPath)) return Results.NotFound();

            var physicalPath = Path.Combine(externalFilesPath, filepath);

            if (!System.IO.File.Exists(physicalPath))
            {
                var filename = res.Info?.Filename;
                var isZip = filename?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true
                            || filename?.EndsWith(".dzi", StringComparison.OrdinalIgnoreCase) == true
                            || filename?.EndsWith(".vsi", StringComparison.OrdinalIgnoreCase) == true;

                if (isZip && System.IO.File.Exists(res.TempFile))
                {
                    try
                    {
                        System.IO.Compression.ZipFile.ExtractToDirectory(res.TempFile, externalFilesPath, overwriteFiles: true);
                    }
                    catch (Exception ex)
                    {
                        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("DocumentEndpoints");
                        logger.LogError(ex, "Failed to unzip cache file {Path} to {Temp}", res.TempFile, externalFilesPath);
                    }
                }
            }

            if (System.IO.File.Exists(physicalPath))
            {
                var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
                if (!contentTypeProvider.TryGetContentType(physicalPath, out var contentType))
                {
                    contentType = "application/octet-stream";
                }

                if (physicalPath.EndsWith(".dzi", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = "application/xml";
                }

                if (filepath.Contains("_files", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Response.Headers.Append("Cache-Control", "public, max-age=31536000");
                }
                else
                {
                    ctx.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                    ctx.Response.Headers.Append("Pragma", "no-cache");
                    ctx.Response.Headers.Append("Expires", "0");
                }

                return Results.File(physicalPath, contentType);
            }

            return Results.NotFound();
        })
        .RequireAuthorization()
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);


        grp.MapPost("upload/{requestId}", async (string requestId, [FromForm] string? parentId, [FromForm] IFormFile file, 
            [FromServices] IMediator mediator, CancellationToken ct) =>
        {
            if (file is not null)
            {
                var fileName = file.FileName;
                var fileSize = file.Length;
                var contentType = file.ContentType;

                Guard.Against.Null(fileSize);

                if (Guid.TryParse(requestId, out var requestGuid))
                {
                    await using Stream stream = file.OpenReadStream();
                    Guid? parguid = Guid.TryParse(parentId, out var p) ? p : null;
                    var req = new UploadDocumentCommand(RequestId: requestGuid, ParentId: parguid, filename: fileName, fileSize: fileSize, fileStream: stream, contenttype: contentType);
                    var node = await mediator.Send(req, ct);
                    return node is null ? Results.NoContent() : Results.Ok(node);
                }
                else
                {
                    return Results.NotFound();
                }
            }
            return Results.NoContent();
        })
            .DisableAntiforgery()
            .Produces<DocumentDto>()
            .RequireAuthorization();

        grp.MapPost("vsi/import", async ([FromBody] WsiImportCommand request, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<WsiImportResponse>()
            .RequireAuthorization("Admin");

        return builder;
    }
}
