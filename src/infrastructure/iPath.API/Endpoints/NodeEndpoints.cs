
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

        grp.MapGet("{id}", (string id, IMediator mediator, CancellationToken ct)
            => mediator.Send(new GetRootNodeByIdQuery(Guid.Parse(id)), ct))
            .Produces<NodeDto>()
            .RequireAuthorization();

        grp.MapPost("list", (GetNodesQuery request, IMediator mediator, CancellationToken ct)
            => mediator.Send(request, ct))
            .Produces<PagedResultList<NodeListDto>>()
            .RequireAuthorization();

        grp.MapPost("idlist", (GetNodeIdListQuery request, IMediator mediator, CancellationToken ct)
            => mediator.Send(request, ct))
            .Produces<IReadOnlyList<Guid>>()
            .RequireAuthorization();

        grp.MapGet("file/{id}", async (string id, IMediator mediator, HttpContext ctx, CancellationToken ct) =>
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
        grp.MapPost("create", (CreateNodeCommand request, IMediator mediator, CancellationToken ct)
            => mediator.Send(request, ct))
            .Produces<NodeListDto>()
            .RequireAuthorization();

        grp.MapDelete("{id}", (string id, IMediator mediator, CancellationToken ct)
            => mediator.Send(new DeleteNodeCommand(Guid.Parse(id)), ct))
            .Produces<NodeDeletedEvent>()
            .RequireAuthorization();

        grp.MapPut("update", (UpdateNodeDescriptionCommand request, IMediator mediator, CancellationToken ct)
            => mediator.Send(request, ct))
            .Produces<bool>()
            .RequireAuthorization();

        grp.MapPut("order", (UpdateChildNodeSortOrderCommand request, IMediator mediator, CancellationToken ct)
            => mediator.Send(request, ct))
            .Produces<ChildNodeSortOrderUpdatedEvent>()
            .RequireAuthorization();

        grp.MapPost("visit/{id}", (string id, IMediator mediator, CancellationToken ct)
            => mediator.Send(new UpdateNodeVisitCommand(Guid.Parse(id)), ct))
            .Produces<bool>()
            .RequireAuthorization();

        grp.MapPost("upload", async ([FromQuery] string rootNodeId, [FromQuery] string? parentNodeId, IFormFile file, IMediator mediator, CancellationToken ct) =>
        {
            if (file is not null)
            {
                var fileName = file.Name;
                var fileSize = file.Length;

                Guard.Against.Null(fileSize);

                if (Guid.TryParse(rootNodeId, out var rootId))
                {
                    Guid parentId = parentNodeId is null ? rootId : Guid.Parse(parentNodeId);

                    await using Stream stream = file.OpenReadStream();
                    var req = new UploadNodeFileCommand(RootNodeId: rootId, ParentNodeId: parentId, filename: fileName, fileSize: fileSize, fileStream: stream);
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
            .RequireAuthorization();


        grp.MapPost("annotation", (CreateNodeAnnotationCommand request, IMediator mediator, CancellationToken ct)
            => mediator.Send(request, ct))
            .Produces<AnnotationDto>()
            .RequireAuthorization();

        grp.MapDelete("annotation/{id}", (string id, IMediator mediator, CancellationToken ct)
            => mediator.Send(new DeleteNodeAnnotationCommand(Guid.Parse(id)), ct))
            .Produces<Guid>()
            .RequireAuthorization();


        return builder;
    }
}
