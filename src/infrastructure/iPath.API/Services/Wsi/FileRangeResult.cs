using iPath.Application.Contracts.Storage;
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

/// <summary>Streams one byte range read from a storage instance, without buffering it.</summary>
public sealed class StreamRangeResult(StreamRange range, string contentType) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = contentType;
        httpContext.Response.ContentLength = range.Length;
        await using var stream = await range.Open(httpContext.RequestAborted);
        await stream.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted);
    }
}

public static class BlobRangeResults
{
    public static IResult For(BlobRange range, string contentType) => range switch
    {
        PhysicalFileRange file => new FileRangeResult(file.Path, file.Offset, file.Length, contentType),
        StreamRange stream => new StreamRangeResult(stream, contentType),
        _ => throw new NotSupportedException($"Unknown range type {range.GetType().Name}"),
    };
}
