using Microsoft.Extensions.Logging;

namespace iPath.API.EndpointFilters;

public class LoggingFilter : IEndpointFilter
{
    private readonly ILogger<LoggingFilter> _logger;

    public LoggingFilter(ILogger<LoggingFilter> logger)
    {
        _logger = logger;
    }
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        _logger.LogInformation(
            "Executing endpoint {Endpoint} with arguments {Args}",
            context.HttpContext.Request.Path,
            context.Arguments);
        var result = await next(context);
        _logger.LogInformation("Finished endpoint with result {@Result}", result);
        return result;
    }
}
