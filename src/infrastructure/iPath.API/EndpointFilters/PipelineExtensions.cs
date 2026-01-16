namespace iPath.API.EndpointFilters;

public static class PipelineExtensions
{
    public static RouteGroupBuilder AddEndpointFilterPipeline(
        this RouteGroupBuilder group)
    {
        return group
            .AddEndpointFilter<ValidationFilter>()
            .AddEndpointFilter<LoggingFilter>();
    }
}