using System.Net;
using System.Net.Http.Headers;
using Refit;

namespace iPath.Blazor.ServiceLib.Services;

public class DirectApiResponse<T> : IApiResponse<T>
{
    public T? Content { get; }
    public bool IsSuccessStatusCode { get; }
    public bool IsSuccessful { get; }
    public HttpStatusCode StatusCode { get; }
    public string? ReasonPhrase { get; }
    public ApiException? Error { get; }
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
            Error = ApiException.Create(error.Message, null!, null!, new HttpResponseMessage(statusCode), null!, error.InnerException).GetAwaiter().GetResult();
        }
    }
}

public class DirectApiResponse : IApiResponse
{
    public bool IsSuccessStatusCode { get; }
    public bool IsSuccessful { get; }
    public HttpStatusCode StatusCode { get; }
    public string? ReasonPhrase { get; }
    public ApiException? Error { get; }
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
            : ApiException.Create(null, null!, null!, new HttpResponseMessage(statusCode), null!, error).GetAwaiter().GetResult();
    }
}
