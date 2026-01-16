using Scalar.AspNetCore;

namespace iPath.API;

public static class QuesionnaireEndpoints
{
    public static IEndpointRouteBuilder MapQuesionnairesApi(this IEndpointRouteBuilder route)
    {
        var qr = route.MapGroup("questionnaires")
            .WithTags("Questionnaires");

        qr.MapGet("{id}", async (string id, int? Version, [FromServices] IMediator mediator, CancellationToken ct)
            =>
        {
            if (Guid.TryParse(id, out var guid))
            {
                return await mediator.Send(new GetQuestionnaireByIdQuery(guid), ct);
            }
            else
            {
                return await mediator.Send(new GetQuestionnaireQuery(id, Version), ct);
            }
        })
            .Produces<QuestionnaireEntity>();


        qr.MapPost("list", async (GetQuestionnaireListQuery query, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(query, ct))
            .Produces<PagedResultList<QuestionnaireListDto>>()
            .RequireAuthorization("Admin");


        qr.MapPost("create", async (UpdateQuestionnaireCommand cmd, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .RequireAuthorization("Admin");

        return route;
    }
}
