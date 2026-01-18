using Ardalis.GuardClauses;
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

                if (res.NotFound)
                {
                    return Results.NotFound();
                }
                else if (res.AccessDenied)
                {
                    return Results.Unauthorized();
                }
                else
                {
                    return Results.File(res.TempFile, contentType: res.Info.MimeType);
                }
            }

            return Results.BadRequest();
        })
           .RequireAuthorization();


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
