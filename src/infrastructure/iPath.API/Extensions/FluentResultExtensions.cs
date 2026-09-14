using Microsoft.AspNetCore.Http;

namespace iPath.API;

public static class FluentResultExtensions
{
    /// <summary>
    /// Maps a failed <see cref="Result{T}"/> to RFC 9457 ProblemDetails, matching the
    /// shape <see cref="Middleware.ExceptionHandlerMiddleware"/> produces, so the UI has
    /// exactly one error format to parse.
    /// </summary>
    public static IResult ToMinimalApiResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return TypedResults.Ok(result.Value);

        var messages = result.Errors.Select(e => e.Message).ToArray();

        return TypedResults.Problem(
            title: "Bad Request",
            detail: messages.FirstOrDefault() ?? "The request could not be completed.",
            statusCode: StatusCodes.Status400BadRequest,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "request_failed",
                ["errors"] = messages
            });
    }
}
