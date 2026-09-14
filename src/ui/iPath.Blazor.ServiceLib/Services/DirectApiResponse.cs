using System.Net;
using System.Net.Http.Headers;
using Refit;

namespace iPath.Blazor.ServiceLib.Services;

public class DirectApiResponse<T> : IApiResponse<T>
{
    public T? Content { get; }
    public bool HasContent => Content is not null;
    public bool IsSuccessfulWithContent => IsSuccessful && Content is not null;
    public bool IsSuccessStatusCode { get; }
    public bool IsSuccessful { get; }
    // In-process (mediator) calls never fail before "a response" exists - there's no network hop to fail on.
    public bool IsReceived => true;
    public HttpStatusCode? StatusCode { get; }
    public string? ReasonPhrase { get; }
    public ApiExceptionBase? Error { get; }
    public HttpRequestMessage? RequestMessage => null;
    public HttpResponseHeaders Headers => _empty.Headers;
    public HttpContentHeaders? ContentHeaders => null;
    public Version Version => HttpVersion.Version10;
    public void Dispose() { }

    private static readonly HttpResponseMessage _empty = new();

    public DirectApiResponse(T? content, bool isSuccess = true, HttpStatusCode statusCode = HttpStatusCode.OK, Exception? error = null)
    {
        Content = content;
        IsSuccessStatusCode = isSuccess;
        IsSuccessful = isSuccess;
        StatusCode = statusCode;
        ReasonPhrase = isSuccess ? "OK" : error?.Message ?? "Error";
        if (error is not null)
        {
            // Refit 15's ApiException.Create dereferences the request message/method to capture
            // request content for diagnostics - unlike older Refit, null here throws instead of
            // being tolerated, so pass harmless placeholders for this synthetic in-process error.
            Error = ApiException.Create(error.Message, new HttpRequestMessage(), HttpMethod.Get,
                new HttpResponseMessage(statusCode), new RefitSettings(), error.InnerException).GetAwaiter().GetResult();
        }
    }

    public bool HasRequestError(out ApiRequestException error)
    {
        error = null!;
        return false;
    }

    public bool HasResponseError(out ApiException error)
    {
        error = (Error as ApiException)!;
        return error is not null;
    }
}

public class DirectApiResponse : IApiResponse
{
    public bool IsSuccessStatusCode { get; }
    public bool IsSuccessful { get; }
    public bool IsReceived => true;
    public HttpStatusCode? StatusCode { get; }
    public string? ReasonPhrase { get; }
    public ApiExceptionBase? Error { get; }
    public HttpRequestMessage? RequestMessage => null;
    public HttpResponseHeaders Headers => _empty.Headers;
    public HttpContentHeaders? ContentHeaders => null;
    public Version Version => HttpVersion.Version10;
    public void Dispose() { }

    private static readonly HttpResponseMessage _empty = new();

    public DirectApiResponse(bool isSuccess = true, HttpStatusCode statusCode = HttpStatusCode.OK, Exception? error = null)
    {
        IsSuccessStatusCode = isSuccess;
        IsSuccessful = isSuccess;
        StatusCode = statusCode;
        ReasonPhrase = isSuccess ? "OK" : error?.Message ?? "Error";
        Error = error is null ? null
            : ApiException.Create(error.Message, new HttpRequestMessage(), HttpMethod.Get,
                new HttpResponseMessage(statusCode), new RefitSettings(), error).GetAwaiter().GetResult();
    }

    public bool HasRequestError(out ApiRequestException error)
    {
        error = null!;
        return false;
    }

    public bool HasResponseError(out ApiException error)
    {
        error = (Error as ApiException)!;
        return error is not null;
    }
}
