using Scalar.AspNetCore;

namespace iPath.API;

public static class QuesionnaireEndpoints
{
    public static IEndpointRouteBuilder MapQuesionnairesApi(this IEndpointRouteBuilder route)
    {
        var qr = route.MapGroup("questionnaires")
            .WithTags("Questionnaires");

        qr.MapGet("{id}", async (string id, IMediator mediator, CancellationToken ct)
            => await mediator.Send(new GetQuestionnaireByIdQuery(Guid.Parse(id)), ct))
            .Produces<Questionnaire>();

        qr.MapPost("list", async (GetQuestionnaireListQuery query, IMediator mediator, CancellationToken ct)
            => await mediator.Send(query, ct))
            .Produces<PagedResultList<QuestionnaireListDto>>()
            .RequireAuthorization("Admin");


        qr.MapPost("create", async (CreateQuestionnaireCommand cmd, IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .RequireAuthorization("Admin");

        return route;
    }
}
