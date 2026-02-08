using iPath.Application.Features.ServiceRequests.Commands;

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
            .Produces<AnnotationDto>(200)
            .RequireAuthorization();

        grp.MapDelete("annotation/{id}", async (string id, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new DeleteAnnotationCommand(Guid.Parse(id)), ct))
            .Produces<Guid>(200)
            .RequireAuthorization();



        // external document import
        grp.MapPost("{id}/uploadfolder", async (string id, [FromServices] IMediator mediator, CancellationToken ct)
             => await mediator.Send(new CreateServiceRequestUploadFolderCommand(Guid.Parse(id)), ct))
            .Produces(200)
            .RequireAuthorization();

        grp.MapDelete("{id}/uploadfolder", async (string id, [FromServices] IMediator mediator, CancellationToken ct)
             => await mediator.Send(new DeleteServiceRequestUploadFolderCommand(Guid.Parse(id)), ct))
            .Produces(200)
            .RequireAuthorization();


        grp.MapGet("{id}/scandocuments", async (string id, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new ScanExternalDocumentsQuery(Guid.Parse(id)), ct))
            .Produces<ScanExternalDocumentResponse>(200)
            .Produces(200)
            .RequireAuthorization();


        grp.MapPost("{id}/importdocuments", async (string id, [FromBody] IReadOnlyList<string>? storageIds, [FromServices] IMediator mediator, CancellationToken ct)
                => await mediator.Send(new ImportExternalDocumentsCommand(Guid.Parse(id), storageIds), ct))
            .Produces<int>(200)
            .Produces(404)
            .RequireAuthorization();



        return builder;
    }
}
