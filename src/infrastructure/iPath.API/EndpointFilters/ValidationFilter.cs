namespace iPath.API.EndpointFilters;

public class ValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        foreach (var arg in context.Arguments)
        {
            if (arg is IValidatable model)
            {
                var errors = model.Validate();
                if (errors.Count > 0)
                    return Results.ValidationProblem(errors);
            }
        }

        return await next(context);
    }
}