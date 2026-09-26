using Microsoft.AspNetCore.Http;

namespace iPath.API.Services.Wsi;

/// <summary>Sends one byte range of a local file, zero-copy via the server's send-file support.</summary>
public sealed class FileRangeResult(string path, long offset, long length, string contentType) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = contentType;
        httpContext.Response.ContentLength = length;
        await httpContext.Response.SendFileAsync(path, offset, length, httpContext.RequestAborted);
    }
}
