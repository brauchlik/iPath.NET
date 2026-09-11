using Ardalis.GuardClauses;
using FluentValidation;
using iPath.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text.Json;

namespace iPath.API.Middleware;

/// <summary>
/// Converts unhandled exceptions on /api/ routes into RFC 9457 ProblemDetails.
/// Non-API requests are rethrown so the Blazor /Error page handles them.
/// </summary>
public class ExceptionHandlerMiddleware(
    RequestDelegate next,
    IHostEnvironment env,
    ILogger<ExceptionHandlerMiddleware> logger)
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted) throw;

            // Let the Blazor error page own non-API failures.
            if (!context.Request.Path.StartsWithSegments("/api")) throw;

            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        // AggregateException hides the real cause, and its own Message
        // ("One or more errors occurred.") is useless to a caller.
        var ex = exception is AggregateException agg && agg.InnerException is not null
            ? agg.InnerException
            : exception;

        var (status, code) = Map(ex);

        if (status >= 500)
            logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            logger.LogInformation("{Code} on {Method} {Path}: {Message}", code, context.Request.Method, context.Request.Path, ex.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = ReasonPhrase(status),
            // Never surface internal exception text to a caller in production.
            Detail = status >= 500 && !env.IsDevelopment()
                ? "An unexpected error occurred."
                : ex.Message,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, _json));
    }

    private static (int Status, string Code) Map(Exception ex) => ex switch
    {
        NotFoundException => ((int)HttpStatusCode.NotFound, "not_found"),
        NotAllowedException => ((int)HttpStatusCode.Forbidden, "forbidden"),
        UnauthorizedAccessException => ((int)HttpStatusCode.Unauthorized, "unauthorized"),
        ValidationException => ((int)HttpStatusCode.BadRequest, "validation_failed"),
        ArgumentException => ((int)HttpStatusCode.BadRequest, "invalid_argument"),
        _ => ((int)HttpStatusCode.InternalServerError, "server_error")
    };

    private static string ReasonPhrase(int status) => status switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        _ => "Server Error"
    };
}
