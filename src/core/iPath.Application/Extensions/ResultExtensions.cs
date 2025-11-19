using FluentResults;

namespace iPath.Application.Extensions;

public static class ResultExtensions
{
    public static string ErrorMessage(this Result result)
        => result.IsSuccess ? string.Empty : string.Join("; ", result.Errors.Select(e => e.Message));
}
