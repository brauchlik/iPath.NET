using Ardalis.GuardClauses;
using iPath.Application.Exceptions;
using System.Net;
using System.Text.Json;

namespace iPath.API.Middleware;

public class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionHandlerMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await ConvertException(context, ex);
        }
    }

    private Task ConvertException(HttpContext context, Exception exception)
    {
        var httpStatusCode = HttpStatusCode.InternalServerError;

        context.Response.ContentType = "application/json";

        var result = string.Empty;

        switch (exception)
        {
            case ArgumentException argumentException:
                httpStatusCode = HttpStatusCode.BadRequest;
                result = JsonSerializer.Serialize(argumentException.Message);
                break;

            case NotAllowedException notAllowed:
                httpStatusCode = HttpStatusCode.Forbidden;
                result = JsonSerializer.Serialize(notAllowed.Message);
                break;

            case AggregateException:
                httpStatusCode = exception.InnerException switch {
                    NotFoundException => HttpStatusCode.NotFound,
                    _ => HttpStatusCode.BadRequest
                };
                break;

            case not null:
                httpStatusCode = HttpStatusCode.BadRequest;
                break;
        }

        context.Response.StatusCode = (int)httpStatusCode;

        if (result == string.Empty) result = JsonSerializer.Serialize(new { error = exception?.Message });

        return context.Response.WriteAsync(result);
    }
}
