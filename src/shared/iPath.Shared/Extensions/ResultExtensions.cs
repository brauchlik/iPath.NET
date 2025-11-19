using FluentResults;

namespace iPath.Shared.Extensions;

public static class ResultExtensions
{
    public static string ErrorMessage(this Result result)
        => result.IsSuccess ? string.Empty : string.Join("; ", result.Errors.Select(e => e.Message));
}
