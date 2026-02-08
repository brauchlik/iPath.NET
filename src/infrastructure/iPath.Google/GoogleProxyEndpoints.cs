using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace iPath.Google;

public static class GoogleProxyEndpoints
{
    public static IEndpointRouteBuilder MapGoogleProxy(this IEndpointRouteBuilder builder)
    {

        var grp = builder.MapGroup("google")
            .WithTags("Google");


        grp.MapGet("/proxy/{docId}", async (string docId, HttpContext context, iPathDbContext db,
            IOptions<GoogleDriveConfig> opts, IHttpClientFactory clientFactory) =>
        {
            var doc = await db.Documents.FindAsync(Guid.Parse(docId));
            // if (doc == null) return Results.NotFound();

            doc.AssertGoogle();

            var client = clientFactory.CreateClient("GoogleDrive");
            var apiKey = opts.Value.PUBLIC_API_KEY;
            var storageId = doc.File.Storage.StorageId;
            var requestUrl = $"https://www.googleapis.com/drive/v3/files/{storageId}?alt=media&key={apiKey}";

            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            // Forward the Range header from GeoTIFF viewer to Google
            if (context.Request.Headers.TryGetValue("Range", out var range))
            {
                request.Headers.TryAddWithoutValidation("Range", range.ToString());
            }

            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            // Map Google's response headers back to our client
            context.Response.StatusCode = (int)response.StatusCode;

            foreach (var header in response.Headers)
                context.Response.Headers[header.Key] = header.Value.ToArray();

            foreach (var header in response.Content.Headers)
                context.Response.Headers[header.Key] = header.Value.ToArray();

            // Add this to ensure the browser knows it can perform range requests
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.Headers["Accept-Ranges"] = "bytes";

            await response.Content.CopyToAsync(context.Response.Body);
        });

        return builder;
    }
}
