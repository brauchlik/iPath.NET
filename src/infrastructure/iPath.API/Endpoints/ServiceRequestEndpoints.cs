
using Ardalis.GuardClauses;
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

        grp.MapPost("list", async (GetServiceRequestsQuery request, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<PagedResultList<ServiceRequestListDto>>()
            .RequireAuthorization();

        grp.MapPost("idlist", async (GetServiceRequestIdListQuery request, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<IReadOnlyList<Guid>>()
            .RequireAuthorization();

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


        return builder;
    }
}
