using DispatchR;
using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;

namespace iPath.API.Endpoints;

public static class TaskAssignmentEndpoints
{
    public static IEndpointRouteBuilder MapTaskAssignmentEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("taskassignments")
            .WithTags("Task Assignments")
            .RequireAuthorization();

        api.MapPost("/my", async (GetUserTaskAssignmentsQuery query, [FromServices] IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(query, ct);
            return Results.Ok(result);
        })
        .Produces<PagedResultList<TaskAssignmentDto>>();

        api.MapGet("/group/{groupId}", async (IMediator mediator, Guid groupId, eTaskStatus? statusFilter, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetGroupTaskAssignmentsQuery(groupId, statusFilter), ct);
            return Results.Ok(result);
        });

        api.MapGet("/case/{serviceRequestId}", async (IMediator mediator, Guid serviceRequestId, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetCaseTaskAssignmentsQuery(serviceRequestId), ct);
            return Results.Ok(result);
        });

        api.MapGet("/{id}", async (IMediator mediator, Guid id, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetTaskAssignmentByIdQuery(id), ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        api.MapPost("/propose", async (IMediator mediator, ProposeTaskAssignmentCommand command, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Created($"/api/v1/taskassignments/{result.Id}", result);
        });

        api.MapPost("/{id}/accept", async (IMediator mediator, Guid id, CancellationToken ct) =>
        {
            var result = await mediator.Send(new AcceptTaskAssignmentCommand(id), ct);
            return Results.Ok(result);
        });

        api.MapPost("/{id}/decline", async (IMediator mediator, Guid id, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeclineTaskAssignmentCommand(id), ct);
            return Results.Ok(result);
        });

        api.MapPost("/{id}/complete", async (IMediator mediator, Guid id, CancellationToken ct) =>
        {
            var result = await mediator.Send(new CompleteTaskAssignmentCommand(id), ct);
            return Results.Ok(result);
        });

        api.MapPost("/{id}/return", async (IMediator mediator, Guid id, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ReturnTaskAssignmentCommand(id), ct);
            return Results.Ok(result);
        });

        api.MapPost("/{id}/cancel", async (IMediator mediator, Guid id, CancellationToken ct) =>
        {
            var result = await mediator.Send(new CancelTaskAssignmentCommand(id), ct);
            return Results.Ok(result);
        });

        api.MapPost("/followup", async (IMediator mediator, CreateFollowUpTaskCommand command, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Created($"/api/v1/taskassignments/{result.Id}", result);
        });

        return app;
    }
}
