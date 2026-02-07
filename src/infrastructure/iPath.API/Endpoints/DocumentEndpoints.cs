using Ardalis.GuardClauses;
using Google.Apis.Drive.v3.Data;
using iPath.Application.Features.Documents;

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

        return builder;
    }
}
