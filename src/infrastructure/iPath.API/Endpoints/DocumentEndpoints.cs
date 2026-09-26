using Ardalis.GuardClauses;
using iPath.API.Services.Wsi;
using iPath.Application.Contracts.Storage;
using iPath.Application.Features.Conversion.Dzi;
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


        grp.MapGet("{id}/{filename}", async (string id, string? filename, [FromServices] IMediator mediator,
            [FromServices] IStorageRegistry storage, CancellationToken ct) =>
        {
            if (!Guid.TryParse(id, out var nodeId))
                return Results.BadRequest();

            var res = await mediator.Send(new GetDocumentFileQuery(nodeId, FetchRemote: false), ct);
            if (res.AccessDenied) return Results.Unauthorized();
            if (res.NotFound) return Results.NotFound();

            return await ServeWholeFileAsync(res, storage, res.Info?.Filename, ct);
        })
           .RequireAuthorization()
           .Produces(StatusCodes.Status200OK)
           .Produces(StatusCodes.Status404NotFound);

        grp.MapGet("files/{*filepath}", async Task<IResult> (string filepath, [FromServices] IMediator mediator,
            [FromServices] DziTileIndexCache tileIndexes, [FromServices] IStorageRegistry storage,
            [FromServices] IOptions<iPath.Domain.Config.iPathConfig> opts, HttpContext ctx, CancellationToken ct) =>
        {
            var request = DziFileRequest.Parse(filepath);
            if (request is null) return Results.BadRequest();

            try
            {
                var res = await mediator.Send(new GetDocumentFileQuery(request.DocumentId, FetchRemote: false), ct);
                if (res.AccessDenied) return Results.Unauthorized();
                if (res.NotFound) return Results.NotFound();

                if (request.Kind == DziFileKind.Raw)
                    return await ServeWholeFileAsync(res, storage, downloadName: null, ct);

                var source = await ResolveTileSourceAsync(res, request.DocumentId, mediator, tileIndexes, storage, ct);
                if (source is null)
                    return ServeLooseDzi(opts.Value.TempDataPath, request, ctx);

                var (index, readRange) = source.Value;
                if (request.Kind == DziFileKind.Descriptor)
                {
                    SetNoCache(ctx);
                    return BlobRangeResults.For(readRange(index.Descriptor), "application/xml");
                }

                if (!index.TryGetTile(request.Level, request.Column, request.Row, out var tile))
                    return Results.NotFound();

                SetTileCache(ctx);
                return BlobRangeResults.For(readRange(tile), TileContentType(index.TileExtension));
            }
            catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
            {
                // The viewer panned on and dropped the request; nothing to answer.
                return Results.Empty;
            }
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

    /// <summary>
    /// A local copy (storage file or temp cache) is sent with zero-copy range support; a file on
    /// a remote instance is streamed from it, range by range when the client asks for ranges.
    /// </summary>
    private static async Task<IResult> ServeWholeFileAsync(FetchFileResponse res, IStorageRegistry storage, string? downloadName, CancellationToken ct)
    {
        var contentType = string.IsNullOrEmpty(res.Info?.MimeType) ? "application/octet-stream" : res.Info.MimeType;

        if (res.ServePath is { } path)
            return Results.File(path, contentType, downloadName, enableRangeProcessing: true);

        if (storage.Resolve(res.StorageInstance) is { } provider && res.StorageKey is { } key
            && await provider.GetLengthAsync(key, ct) is { } length)
            return new RemoteFileResult(provider, key, length, contentType, downloadName);

        return Results.NotFound();
    }

    /// <summary>
    /// The tile index and how to read a range for a DZI zip: from a local copy when there is one,
    /// else straight from the remote instance using the stored index. A remote zip without a
    /// stored index is fetched into the temp cache once and indexed there.
    /// </summary>
    private static async Task<(DziTileIndex Index, Func<ZipRange, BlobRange> ReadRange)?> ResolveTileSourceAsync(
        FetchFileResponse res, Guid documentId, IMediator mediator, DziTileIndexCache tileIndexes, IStorageRegistry storage, CancellationToken ct)
    {
        if (res.ServePath is { } path)
        {
            var local = await tileIndexes.GetAsync(path, ct);
            return local is null ? null : (local, r => new PhysicalFileRange(path, r.Offset, r.Length));
        }

        if (storage.Resolve(res.StorageInstance) is not { } provider || res.StorageKey is not { } key)
            return null;

        var remote = await tileIndexes.GetAsync(provider, key, ct);
        if (remote is not null)
            return (remote, r => provider.GetRange(key, r.Offset, r.Length));

        var fetched = await mediator.Send(new GetDocumentFileQuery(documentId, FetchRemote: true), ct);
        if (fetched.ServePath is not { } cached)
            return null;
        var index = await tileIndexes.GetAsync(cached, ct);
        return index is null ? null : (index, r => new PhysicalFileRange(cached, r.Offset, r.Length));
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
