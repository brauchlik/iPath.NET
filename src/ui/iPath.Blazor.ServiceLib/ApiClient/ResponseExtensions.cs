using FluentResults;
using Refit;

namespace iPath.Blazor.ServiceLib.ApiClient;

public static class ResponseExtensions
{
    public static Result<T> ToResult<T>(this IApiResponse<T>? response) where T : class
    {
        if (response == null)
        {
            return Result.Fail("no response");
        }
        else if (!response.IsSuccessful)
        {
            return Result.Fail(response.Error.Message);
        }
        else
        {
            return new Result<T>().WithValue(response.Content);
        }
    }
}
