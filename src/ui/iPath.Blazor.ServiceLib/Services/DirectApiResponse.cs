using System.Net;
using System.Net.Http.Headers;
using Refit;

namespace iPath.Blazor.ServiceLib.Services;

public class DirectApiResponse<T>(T? content, bool isSuccess = true, HttpStatusCode statusCode = HttpStatusCode.OK) : IApiResponse<T>
{
    public T? Content => content;
    public bool IsSuccessStatusCode => isSuccess;
    public bool IsSuccessful => isSuccess;
    public HttpStatusCode StatusCode => statusCode;
    public string? ReasonPhrase => isSuccess ? "OK" : "Error";
    public HttpRequestMessage? RequestMessage => null;
    public HttpResponseHeaders Headers => _empty.Headers;
    public HttpContentHeaders? ContentHeaders => null;
    public Version Version => HttpVersion.Version10;
    public ApiException? Error => null;
    public void Dispose() { }

    private static readonly HttpResponseMessage _empty = new();
}

public class DirectApiResponse(bool isSuccess = true, HttpStatusCode statusCode = HttpStatusCode.OK) : IApiResponse
{
    public bool IsSuccessStatusCode => isSuccess;
    public bool IsSuccessful => isSuccess;
    public HttpStatusCode StatusCode => statusCode;
    public string? ReasonPhrase => isSuccess ? "OK" : "Error";
    public HttpRequestMessage? RequestMessage => null;
    public HttpResponseHeaders Headers => _empty.Headers;
    public HttpContentHeaders? ContentHeaders => null;
    public Version Version => HttpVersion.Version10;
    public ApiException? Error => null;
    public void Dispose() { }

    private static readonly HttpResponseMessage _empty = new();
}
