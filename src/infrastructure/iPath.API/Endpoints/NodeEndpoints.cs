
using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Mvc;

namespace iPath.API.Endpoints;

public static class NodeEndpoints
{
    public static IEndpointRouteBuilder MapNodeEndpoints(this IEndpointRouteBuilder builder)
    {
        var grp = builder.MapGroup("nodes")
            .WithTags("Nodes");

        // Queries

        grp.MapGet("{id}", async (string id, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new GetRootNodeByIdQuery(Guid.Parse(id)), ct))
            .Produces<NodeDto>()
            .RequireAuthorization();

        grp.MapPost("list", async (GetNodesQuery request, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<PagedResultList<NodeListDto>>()
            .RequireAuthorization();

        grp.MapPost("idlist", async (GetNodeIdListQuery request, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<IReadOnlyList<Guid>>()
            .RequireAuthorization();

        grp.MapGet("file/{id}", async (string id, [FromServices] IMediator mediator, HttpContext ctx, CancellationToken ct) =>
        {
            if (Guid.TryParse(id, out var nodeId))
            {
                var res = await mediator.Send(new GetNodeFileQuery(nodeId), ct);

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


        // Commands
        grp.MapPost("create", async (CreateNodeCommand request, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<NodeDto>()
            .RequireAuthorization();

        grp.MapDelete("{id}", async (string id, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new DeleteNodeCommand(Guid.Parse(id)), ct))
            .Produces<NodeDeletedEvent>()
            .RequireAuthorization();

        grp.MapPut("update", async (UpdateNodeCommand request, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<bool>()
            .RequireAuthorization();

        grp.MapPut("order", async (UpdateChildNodeSortOrderCommand request, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<ChildNodeSortOrderUpdatedEvent>()
            .RequireAuthorization();

        grp.MapPost("visit/{id}", async (string id, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new UpdateNodeVisitCommand(Guid.Parse(id)), ct))
            .Produces<bool>()
            .RequireAuthorization();

        grp.MapPost("upload/{id}", async (string id, [FromForm] IFormFile file, 
            [FromServices] IMediator mediator, CancellationToken ct) =>
        {
            if (file is not null)
            {
                var fileName = file.FileName;
                var fileSize = file.Length;
                var contentType = file.ContentType;

                Guard.Against.Null(fileSize);

                if (Guid.TryParse(id, out var parentId))
                {
                    await using Stream stream = file.OpenReadStream();
                    var req = new UploadNodeFileCommand(ParentNodeId: parentId, filename: fileName, fileSize: fileSize, fileStream: stream, contenttype: contentType);
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
            .Produces<NodeDto>()
            .RequireAuthorization();


        grp.MapPost("annotation", async (CreateNodeAnnotationCommand request, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<AnnotationDto>()
            .RequireAuthorization();

        grp.MapDelete("annotation/{id}", async (string id, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new DeleteNodeAnnotationCommand(Guid.Parse(id)), ct))
            .Produces<Guid>()
            .RequireAuthorization();


        return builder;
    }
}
