using Ardalis.GuardClauses;
using iPath.API.Services.Wsi;
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


        grp.MapGet("{id}/{filename}", async (string id, string? filename, [FromServices] IMediator mediator, CancellationToken ct) =>
        {
            if (!Guid.TryParse(id, out var nodeId))
                return Results.BadRequest();

            var res = await mediator.Send(new GetDocumentFileQuery(nodeId), ct);
            if (res.AccessDenied) return Results.Unauthorized();

            var path = res.ServePath;
            if (res.NotFound || path is null) return Results.NotFound();

            return Results.File(path, contentType: res.Info?.MimeType, fileDownloadName: res.Info?.Filename, enableRangeProcessing: true);
        })
           .RequireAuthorization()
           .Produces(StatusCodes.Status200OK)
           .Produces(StatusCodes.Status404NotFound);

        grp.MapGet("files/{*filepath}", async Task<IResult> (string filepath, [FromServices] IMediator mediator,
            [FromServices] DziTileIndexCache tileIndexes, [FromServices] IOptions<iPath.Domain.Config.iPathConfig> opts,
            HttpContext ctx, CancellationToken ct) =>
        {
            var request = DziFileRequest.Parse(filepath);
            if (request is null) return Results.BadRequest();

            var res = await mediator.Send(new GetDocumentFileQuery(request.DocumentId), ct);
            if (res.AccessDenied) return Results.Unauthorized();

            var path = res.ServePath;
            if (res.NotFound || path is null) return Results.NotFound();

            if (request.Kind == DziFileKind.Raw)
                return Results.File(path, contentType: res.Info?.MimeType ?? "application/octet-stream", enableRangeProcessing: true);

            var index = await tileIndexes.GetAsync(path, ct);
            if (index is null)
                return ServeLooseDzi(opts.Value.TempDataPath, request, ctx);

            if (request.Kind == DziFileKind.Descriptor)
            {
                SetNoCache(ctx);
                return new FileRangeResult(path, index.Descriptor.Offset, index.Descriptor.Length, "application/xml");
            }

            if (!index.TryGetTile(request.Level, request.Column, request.Row, out var tile))
                return Results.NotFound();

            SetTileCache(ctx);
            return new FileRangeResult(path, tile.Offset, tile.Length, TileContentType(index.TileExtension));
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

    // WsiConversionPlugin still writes its dzsave output unzipped into TempDataPath.
    private static IResult ServeLooseDzi(string tempDataPath, DziFileRequest request, HttpContext ctx)
    {
        var path = request.LoosePath(tempDataPath);
        if (!System.IO.File.Exists(path))
            return Results.NotFound();

        if (request.Kind == DziFileKind.Descriptor)
        {
            SetNoCache(ctx);
            return Results.File(path, "application/xml");
        }

        SetTileCache(ctx);
        return Results.File(path, TileContentType(request.Extension!));
    }

    private static void SetNoCache(HttpContext ctx)
    {
        ctx.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        ctx.Response.Headers.Pragma = "no-cache";
        ctx.Response.Headers.Expires = "0";
    }

    // Tiles never change for a document id, so the browser keeps them. "private": they are
    // access-controlled patient data and must not be stored by shared proxies.
    private static void SetTileCache(HttpContext ctx) =>
        ctx.Response.Headers.CacheControl = "private, max-age=31536000, immutable";

    private static string TileContentType(string extension) => extension switch
    {
        "webp" => "image/webp",
        "png" => "image/png",
        _ => "image/jpeg",
    };
}
