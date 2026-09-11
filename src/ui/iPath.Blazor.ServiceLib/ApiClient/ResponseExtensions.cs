using FluentResults;
using Refit;
using System.Text.Json;

namespace iPath.Blazor.ServiceLib.ApiClient;

/// <summary>
/// Turns a Refit response into something the UI can act on. The API returns RFC 9457
/// ProblemDetails for every failure, so the readable message lives in "detail" and a
/// stable machine-readable key in "code" — never show the raw body to a user.
/// </summary>
public static class ResponseExtensions
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Human-readable message for a failed response, safe to show in a snackbar.</summary>
    public static string ErrorText(this IApiResponse? response)
    {
        if (response is null) return "No response from the server.";
        if (response.IsSuccessful) return string.Empty;

        var problem = Parse(response.Error?.Content);
        if (!string.IsNullOrWhiteSpace(problem?.Detail)) return problem!.Detail!;
        if (!string.IsNullOrWhiteSpace(problem?.Title)) return problem!.Title!;

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "You are not signed in.",
            System.Net.HttpStatusCode.Forbidden => "You do not have permission to do that.",
            System.Net.HttpStatusCode.NotFound => "Not found.",
            _ => "The request could not be completed."
        };
    }

    /// <summary>Stable error key for branching or localization lookup, e.g. "not_found".</summary>
    public static string? ErrorCode(this IApiResponse? response)
        => response is null || response.IsSuccessful ? null : Parse(response.Error?.Content)?.Code;

    public static Result<T> ToResult<T>(this IApiResponse<T>? response) where T : class
    {
        if (response is null) return Result.Fail("No response from the server.");
        if (!response.IsSuccessful) return Result.Fail(new Error(response.ErrorText())
            .WithMetadata("code", response.ErrorCode() ?? "unknown")
            .WithMetadata("status", (int)response.StatusCode));

        return Result.Ok(response.Content!);
    }

    private static ProblemPayload? Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        try { return JsonSerializer.Deserialize<ProblemPayload>(content, _json); }
        catch (JsonException) { return null; }
    }

    private sealed class ProblemPayload
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public string? Code { get; set; }
        public string[]? Errors { get; set; }
    }
}
