using iPath.Application.Contracts.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace iPath.API.Services.Wsi;

/// <summary>
/// Serves an object from a remote storage instance, honouring a single HTTP byte range, so
/// range-reading clients (e.g. a GeoTIFF viewer on an original slide) never make the server
/// download the whole object.
/// </summary>
public sealed class RemoteFileResult(IStorageProvider provider, string key, long length, string contentType, string? downloadName) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var response = httpContext.Response;
        response.Headers.AcceptRanges = "bytes";
        response.ContentType = contentType;
        if (!string.IsNullOrEmpty(downloadName))
        {
            var disposition = new ContentDispositionHeaderValue("attachment");
            disposition.SetHttpFileName(downloadName);
            response.Headers.ContentDisposition = disposition.ToString();
        }

        var (offset, count, partial) = ParseRange(httpContext.Request, length);
        if (count < 0)
        {
            response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
            response.Headers.ContentRange = new ContentRangeHeaderValue(length).ToString();
            return;
        }

        if (partial)
        {
            response.StatusCode = StatusCodes.Status206PartialContent;
            response.Headers.ContentRange = new ContentRangeHeaderValue(offset, offset + count - 1, length).ToString();
        }
        response.ContentLength = count;
        if (count == 0 || HttpMethods.IsHead(httpContext.Request.Method))
            return;

        if (provider.GetRange(key, offset, count) is not StreamRange range)
            throw new InvalidOperationException($"Storage '{provider.InstanceName}' is not a remote instance.");
        await using var stream = await range.Open(httpContext.RequestAborted);
        await stream.CopyToAsync(response.Body, httpContext.RequestAborted);
    }

    /// <returns>Count -1 when the requested range cannot be satisfied.</returns>
    public static (long Offset, long Count, bool Partial) ParseRange(HttpRequest request, long length)
    {
        // Only a single range is supported; multipart ranges get the whole object.
        if (!RangeHeaderValue.TryParse(request.Headers.Range.ToString(), out var header)
            || !string.Equals(header.Unit.Value, "bytes", StringComparison.OrdinalIgnoreCase)
            || header.Ranges.Count != 1)
            return (0, length, false);

        var range = header.Ranges.First();
        long start, end;
        if (range.From is { } from)
        {
            start = from;
            end = Math.Min(range.To ?? length - 1, length - 1);
        }
        else if (range.To is { } suffix)
        {
            start = Math.Max(0, length - suffix);
            end = length - 1;
        }
        else
        {
            return (0, length, false);
        }

        return start >= length || start > end ? (0, -1, true) : (start, end - start + 1, true);
    }
}
