
using Ardalis.GuardClauses;
using DispatchR;
using iPath.Application.Features.Documents;
using Microsoft.AspNetCore.Mvc;

namespace iPath.API.Endpoints;

public static class ServiceRequestEndpoints
{
    public static IEndpointRouteBuilder MapServiceRequestEndpoints(this IEndpointRouteBuilder builder)
    {
        var grp = builder.MapGroup("requests")
            .WithTags("Service Requests");

        // Queries

        grp.MapGet("{id}", async (string id, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new GetServiceRequestByIdQuery(Guid.Parse(id)), ct))
            .Produces<ServiceRequestDto>()
            .RequireAuthorization();

        grp.MapPost("list", async (GetServiceRequestListQuery request, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<PagedResultList<ServiceRequestListDto>>()
            .RequireAuthorization();

        grp.MapPost("idlist", async (GetServiceRequestIdListQuery request, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<IReadOnlyList<Guid>>()
            .RequireAuthorization();


        grp.MapGet("updates", async ([FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new GetServiceRequestUpdatesQuery(), ct))
            .Produces<ServiceRequestUpdatesDto>()
            .RequireAuthorization();

        /*
        grp.MapGet("new", async ([FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new GetNewServiceRequestsQuery(), ct))
            .Produces<PagedResultList<ServiceRequestListDto>>()
            .WithName("New Requests")
            .RequireAuthorization();

        grp.MapGet("newannotations", async ([FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new GetServiceRequestsWithNewAnnotationsQuery(), ct))
            .Produces<PagedResultList<ServiceRequestListDto>>()
            .WithName("New Annotations")
            .RequireAuthorization();
        */

        // Commands
        grp.MapPost("create", async (CreateServiceRequestCommand request, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<ServiceRequestDto>()
            .RequireAuthorization();

        grp.MapDelete("{id}", async (string id, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new DeleteServiceRequestCommand(Guid.Parse(id)), ct))
            .Produces<ServiceRequestDeletedEvent>()
            .RequireAuthorization();

        grp.MapPut("update", async (UpdateServiceRequestCommand request, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<bool>()
            .RequireAuthorization();

        grp.MapPost("visit/{id}", async (string id, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new UpdateServiceRequestVisitCommand(Guid.Parse(id)), ct))
            .Produces<bool>()
            .RequireAuthorization();


        // Annotations
        grp.MapPost("annotation", async (CreateAnnotationCommand request, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<AnnotationDto>()
            .RequireAuthorization();

        grp.MapDelete("annotation/{id}", async (string id, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new DeleteAnnotationCommand(Guid.Parse(id)), ct))
            .Produces<Guid>()
            .RequireAuthorization();



        // dev
        grp.MapGet("{id}/scan", async (string id, [FromServices] IRemoteStorageService srv) => 
        {
            if (Guid.TryParse(id, out var srid))
            {
                var res = await srv.ScanNewFilesAsync(srid);
                return Results.Ok(res);
            }
            return Results.NotFound();
        });

        return builder;
    }
}
